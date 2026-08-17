using Cartex.Application.Common.Finance;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Customers.Commands;
using Cartex.Application.Stocks.Queries;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Cartex.Shared.Models.Stocks;

namespace Cartex.Application.Tests;

[Collection("database")]
public class StockDiscountBadgeTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private sealed class NoStorage : IObjectStorage
    {
        public Task<string> UploadAsync(Stream content, long length, string contentType, string extension, CancellationToken cancellationToken = default, string? key = null) =>
            throw new NotSupportedException();

        public Task<string?> GetUrlAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);

        public Task<IReadOnlyDictionary<string, string>> GetUrlsAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());

        public Task<(Stream Content, string ContentType)?> DownloadAsync(string key, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task DeleteAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private async Task<(long branch1, long warehouse1, long businessId, long adminId, long productId, long variantId)> SetupAsync(bool enableFeature = true)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Features.Where(f => f.Code == FeatureCatalog.Loyalty)
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.IsEnabled, enableFeature));
        var branch1 = (await db.Branches.FirstAsync(b => b.Name == "Asosiy filial")).Id;
        var warehouse1 = (await db.Warehouses.FirstAsync(w => w.Name == "Asosiy ombor")).Id;
        var businessId = (await db.Businesses.FirstAsync()).Id;
        var adminId = (await db.Users.FirstAsync(u => u.Username == "admin")).Id;
        var productId = (await db.Products.FirstAsync(p => p.Name == "Smesitel oshxona Zegor")).Id;
        var variantId = (await db.ProductVariants.FirstAsync(v => v.ProductId == productId)).Id;
        return (branch1, warehouse1, businessId, adminId, productId, variantId);
    }

    private async Task AddRuleAsync(DiscountRule rule)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.DiscountRules.Add(rule);
        await db.SaveChangesAsync();
    }

    private async Task<IReadOnlyCollection<StockOnHandDto>> ItemsAsync(long warehouseId)
    {
        using var scope = Fixture.CreateScope();
        var handler = new GetStockOnHandQueryHandler(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(),
            new NoStorage(),
            scope.ServiceProvider.GetRequiredService<IFeatureStateProvider>(),
            scope.ServiceProvider.GetRequiredService<ISettingsService>(),
            scope.ServiceProvider.GetRequiredService<ICurrencyService>());
        var page = await handler.Handle(new GetStockOnHandQuery(warehouseId), default);
        return page.Items;
    }

    [Fact]
    public async Task All_scope_percent_rule_marks_every_item()
    {
        var (branch1, warehouse1, businessId, adminId, _, _) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);

        await AddRuleAsync(new DiscountRule { Name = "Hammaga", Scope = DiscountScope.All, Method = DiscountMethod.Percent, Value = 10 });

        var items = await ItemsAsync(warehouse1);

        Assert.NotEmpty(items);
        Assert.All(items, i => Assert.Equal(10m, i.DiscountPct));
    }

    [Fact]
    public async Task Product_rule_beats_all_scope_rule_on_its_product()
    {
        var (branch1, warehouse1, businessId, adminId, productId, variantId) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);

        await AddRuleAsync(new DiscountRule { Name = "Hammaga", Scope = DiscountScope.All, Method = DiscountMethod.Percent, Value = 10 });
        await AddRuleAsync(new DiscountRule { Name = "Smesitel", Scope = DiscountScope.Product, TargetId = productId, Method = DiscountMethod.Percent, Value = 15 });

        var items = await ItemsAsync(warehouse1);

        Assert.Equal(15m, items.First(i => i.VariantId == variantId).DiscountPct);
        Assert.All(items.Where(i => i.VariantId != variantId), i => Assert.Equal(10m, i.DiscountPct));
    }

    [Fact]
    public async Task Rule_with_min_amount_is_ignored()
    {
        var (branch1, warehouse1, businessId, adminId, _, _) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);

        await AddRuleAsync(new DiscountRule { Name = "Chek bo'yicha", Scope = DiscountScope.All, Method = DiscountMethod.Percent, Value = 10, MinAmount = 50_000m });

        var items = await ItemsAsync(warehouse1);

        Assert.NotEmpty(items);
        Assert.All(items, i => Assert.Null(i.DiscountPct));
    }

    [Fact]
    public async Task Customer_rule_is_ignored()
    {
        var (branch1, warehouse1, businessId, adminId, _, _) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);

        long customerId;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            customerId = await sender.Send(new CreateCustomerCommand("Shaxsiy Mijoz", "+998" + Random.Shared.NextInt64(100_000_000, 999_999_999), null, 0m));
        }

        await AddRuleAsync(new DiscountRule { Name = "Faqat mijozga", Scope = DiscountScope.All, Method = DiscountMethod.Percent, Value = 10, CustomerId = customerId });

        var items = await ItemsAsync(warehouse1);

        Assert.NotEmpty(items);
        Assert.All(items, i => Assert.Null(i.DiscountPct));
    }

    [Fact]
    public async Task Fixed_amount_rule_is_ignored()
    {
        var (branch1, warehouse1, businessId, adminId, _, _) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);

        await AddRuleAsync(new DiscountRule { Name = "Belgilangan summa", Scope = DiscountScope.All, Method = DiscountMethod.FixedAmount, Value = 5_000m });

        var items = await ItemsAsync(warehouse1);

        Assert.NotEmpty(items);
        Assert.All(items, i => Assert.Null(i.DiscountPct));
    }

    [Fact]
    public async Task Expired_rule_is_ignored()
    {
        var (branch1, warehouse1, businessId, adminId, _, _) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);

        await AddRuleAsync(new DiscountRule
        {
            Name = "Tugagan aksiya",
            Scope = DiscountScope.All,
            Method = DiscountMethod.Percent,
            Value = 10,
            EndsOn = DateOnly.FromDateTime(DateTime.Now).AddDays(-1)
        });

        var items = await ItemsAsync(warehouse1);

        Assert.NotEmpty(items);
        Assert.All(items, i => Assert.Null(i.DiscountPct));
    }

    [Fact]
    public async Task Exception_clears_badge_on_excluded_product()
    {
        var (branch1, warehouse1, businessId, adminId, productId, variantId) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);

        await AddRuleAsync(new DiscountRule
        {
            Name = "Hammaga",
            Scope = DiscountScope.All,
            Method = DiscountMethod.Percent,
            Value = 10,
            Exceptions = [new DiscountRuleException { Scope = DiscountScope.Product, TargetId = productId }]
        });

        var items = await ItemsAsync(warehouse1);

        Assert.Null(items.First(i => i.VariantId == variantId).DiscountPct);
        Assert.All(items.Where(i => i.VariantId != variantId), i => Assert.Equal(10m, i.DiscountPct));
    }

    [Fact]
    public async Task Disabled_loyalty_feature_clears_all_badges()
    {
        var (branch1, warehouse1, businessId, adminId, _, _) = await SetupAsync(enableFeature: false);
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);

        await AddRuleAsync(new DiscountRule { Name = "Hammaga", Scope = DiscountScope.All, Method = DiscountMethod.Percent, Value = 10 });

        var items = await ItemsAsync(warehouse1);

        Assert.NotEmpty(items);
        Assert.All(items, i => Assert.Null(i.DiscountPct));
    }
}
