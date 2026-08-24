using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Shared.Models.Products;

namespace Cartex.Application.Settings.Queries;

public sealed record GetProductReferenceSettingsQuery : IRequest<ProductReferenceSettingsDto>;

public sealed class GetProductReferenceSettingsQueryHandler(ISettingsService settings)
    : IRequestHandler<GetProductReferenceSettingsQuery, ProductReferenceSettingsDto>
{
    public async Task<ProductReferenceSettingsDto> Handle(GetProductReferenceSettingsQuery request, CancellationToken cancellationToken)
    {
        var config = await settings.GetAsync<ProductReferenceSettings>(SettingKeys.ProductReference, cancellationToken)
            ?? new ProductReferenceSettings();
        return ProductReferenceSettingsMapping.ToDto(config);
    }
}

internal static class ProductReferenceSettingsMapping
{
    public static ProductReferenceSettingsDto ToDto(ProductReferenceSettings config) => new()
    {
        IsEnabled = config.IsEnabled,
        SourceType = config.SourceType,
        SpreadsheetId = config.SpreadsheetId,
        SheetName = config.SheetName,
        BarcodeColumn = config.BarcodeColumn,
        NameColumn = config.NameColumn,
        UnitColumn = config.UnitColumn,
        CategoryColumn = config.CategoryColumn,
        ManufacturerColumn = config.ManufacturerColumn,
        PackQtyColumn = config.PackQtyColumn,
        PriceColumn = config.PriceColumn,
        AutoFillPrice = config.AutoFillPrice,
        SyncSchedule = config.SyncSchedule,
        LastSyncedAt = config.LastSyncedAt,
        LastReadCount = config.LastReadCount,
        LastUpdatedCount = config.LastUpdatedCount,
        LastErrorCount = config.LastErrorCount,
        RowCount = config.RowCount,
        LastError = config.LastError
    };
}
