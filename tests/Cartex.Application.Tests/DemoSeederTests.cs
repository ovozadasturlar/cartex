using Cartex.Application.Tests.Common;
using Cartex.Domain.Common;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace Cartex.Application.Tests;

public sealed class DemoSeederTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    private ServiceProvider _services = null!;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ICurrentUser>(new TestCurrentUser());
        services.AddPersistence(_container.GetConnectionString());
        services.AddApplication();
        _services = services.BuildServiceProvider();

        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        await DatabaseSeeder.SeedAsync(db, p => p);
        await DemoDataSeeder.SeedAsync(db);
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task Seeds_realistic_sales_with_consistent_ledger_and_populated_pages()
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var sales = await db.Sales.ToListAsync();
        Assert.True(sales.Count > 0);
        var distinctDays = sales.Select(s => s.CreatedAt.Date).Distinct().Count();
        Assert.True(distinctDays >= 15, $"distinct sale days = {distinctDays}");
        Assert.All(sales, s => Assert.Equal(s.TotalAmount, s.PaidCash + s.PaidCard + s.PaidBonus + s.DebtAmount));

        var accounts = await db.Accounts.ToListAsync();
        var transactions = await db.Transactions.ToListAsync();
        Assert.NotEmpty(transactions);

        foreach (var acc in accounts)
        {
            var inflow = transactions.Where(t => t.ToAccountId == acc.Id).Sum(t => t.Amount);
            var outflow = transactions.Where(t => t.FromAccountId == acc.Id).Sum(t => t.Amount);
            Assert.True(Math.Abs(acc.Balance - (inflow - outflow)) < 0.01m,
                $"Account {acc.Id} ({acc.Type}) balance {acc.Balance} != ledger {inflow - outflow}");
            if (acc.SupplierId is null)
                Assert.True(acc.Balance >= 0m, $"Account {acc.Id} ({acc.Type}) balance negative: {acc.Balance}");
        }

        Assert.True(await db.Stocks.AnyAsync());
        Assert.True(await db.Customers.CountAsync() >= 5);
        Assert.True(await db.Suppliers.AnyAsync());
        Assert.True(await db.Supplies.AnyAsync());
        Assert.True(await db.StockTransfers.AnyAsync());
        Assert.True(await db.Shifts.AnyAsync(s => s.Status == ShiftStatus.Open));
        Assert.True(await db.Shifts.AnyAsync(s => s.Status == ShiftStatus.Closed));
    }
}
