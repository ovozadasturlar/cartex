using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Settings;
using Cartex.Application.ProductReference.Commands;
using Cartex.Application.ProductReference.Queries;
using Cartex.Application.Products.Queries;
using Cartex.Application.Tests.Common;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public sealed class ProductReferenceCatalogTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task MAKAT_01_Product_catalog_hit_does_not_fetch_reference_source()
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var code = await db.Barcodes.Select(barcode => barcode.Code).FirstAsync();

        var product = await sender.Send(new GetProductByBarcodeQuery(code, 0));

        Assert.NotNull(product);
        Assert.Equal(0, Fixture.ProductReferenceSource.FetchCount);
    }

    [Fact]
    public async Task MAKAT_05_Sync_upserts_and_deduplicates_without_deleting_stale_rows()
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        await settings.SetAsync(SettingKeys.ProductReference, EnabledSettings());
        db.ProductReferences.Add(new Cartex.Domain.Entities.ProductReference
        {
            Barcode = "old",
            Name = "Old row",
            SourceKey = "old-sheet",
            SyncedAt = DateTime.UtcNow.AddDays(-1)
        });
        await db.SaveChangesAsync();
        Fixture.ProductReferenceSource.Rows =
        [
            new("same", "First", null, null, null, null, null, "sheet"),
            new("same", "Second", "dona", null, null, 6, 12_000, "sheet")
        ];

        var first = await sender.Send(new SyncProductReferenceCommand());
        Fixture.ProductReferenceSource.Rows = [];
        var second = await sender.Send(new SyncProductReferenceCommand());

        Assert.Equal(2, first.Read);
        Assert.Equal(1, first.Updated);
        Assert.Equal(0, first.Errors);
        Assert.Equal(2, second.Total);
        Assert.Equal(2, await db.ProductReferences.CountAsync());
        Assert.Equal("Second", await db.ProductReferences.Where(x => x.Barcode == "same").Select(x => x.Name).SingleAsync());
        Assert.True(await db.ProductReferences.AnyAsync(x => x.Barcode == "old"));
    }

    [Fact]
    public async Task MAKAT_05_Invalid_rows_are_skipped_and_counted()
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        await settings.SetAsync(SettingKeys.ProductReference, EnabledSettings());
        Fixture.ProductReferenceSource.Rows =
        [
            new(null, "No barcode", null, null, null, null, null, "sheet"),
            new("empty-name", " ", null, null, null, null, null, "sheet"),
            new("valid", "Valid", null, null, null, null, null, "sheet")
        ];

        var result = await sender.Send(new SyncProductReferenceCommand());

        Assert.Equal(3, result.Read);
        Assert.Equal(1, result.Updated);
        Assert.Equal(2, result.Errors);
    }

    [Fact]
    public async Task MAKAT_07_Disabled_feature_does_not_fetch_source_or_return_reference()
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.ProductReferences.Add(new Cartex.Domain.Entities.ProductReference
        {
            Barcode = "disabled",
            Name = "Hidden",
            SourceKey = "sheet",
            SyncedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        await Assert.ThrowsAnyAsync<Exception>(() => sender.Send(new SyncProductReferenceCommand()));
        var lookup = await sender.Send(new GetProductReferenceByBarcodeQuery("disabled"));

        Assert.Equal(0, Fixture.ProductReferenceSource.FetchCount);
        Assert.Null(lookup);
    }

    [Fact]
    public async Task MAKAT_04_Price_is_returned_only_when_auto_fill_is_enabled()
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        db.ProductReferences.Add(new Cartex.Domain.Entities.ProductReference
        {
            Barcode = "price",
            Name = "Priced",
            SuggestedPrice = 12_000,
            SourceKey = "sheet",
            SyncedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        await settings.SetAsync(SettingKeys.ProductReference, EnabledSettings());

        var disabled = await sender.Send(new GetProductReferenceByBarcodeQuery("price"));
        await settings.SetAsync(SettingKeys.ProductReference, EnabledSettings() with { AutoFillPrice = true });
        var enabled = await sender.Send(new GetProductReferenceByBarcodeQuery("price"));

        Assert.NotNull(disabled);
        Assert.Null(disabled.SuggestedPrice);
        Assert.Equal(12_000, enabled?.SuggestedPrice);
    }

    private static Cartex.Shared.Models.Products.ProductReferenceSettingsDto EnabledSettings() => new()
    {
        IsEnabled = true,
        SpreadsheetId = "sheet-id",
        SheetName = "Products"
    };
}
