using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Shared.Models.Catalog;
using Microsoft.Extensions.Logging;

namespace Cartex.Application.Catalog;

public sealed class CatalogReference(
    ISettingsService settings,
    IEnumerable<ICatalogSource> sources,
    ILogger<CatalogReference> logger) : ICatalogReference
{
    public const int MinQueryLength = 3;
    public const int MaxLimit = 25;

    public async Task<CatalogProductDto?> ByBarcodeAsync(string? barcode, CancellationToken cancellationToken = default)
    {
        var code = barcode?.Trim();
        if (string.IsNullOrEmpty(code))
            return null;

        var source = await SourceAsync(cancellationToken);
        if (source is null)
            return null;

        try
        {
            return await source.ByBarcodeAsync(code, cancellationToken);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            logger.LogWarning(failure, "Catalog reference barcode lookup failed ({Mode})", source.Mode);
            return null;
        }
    }

    public async Task<IReadOnlyList<CatalogProductDto>> SearchAsync(string? query, int limit, CancellationToken cancellationToken = default)
    {
        var text = query?.Trim();
        if (text is null || text.Length < MinQueryLength)
            return [];

        var source = await SourceAsync(cancellationToken);
        if (source is null)
            return [];

        try
        {
            return await source.SearchAsync(text, Math.Clamp(limit, 1, MaxLimit), cancellationToken);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            logger.LogWarning(failure, "Catalog reference search failed ({Mode})", source.Mode);
            return [];
        }
    }

    private async Task<ICatalogSource?> SourceAsync(CancellationToken cancellationToken)
    {
        var config = await settings.GetAsync<ProductReferenceSettings>(SettingKeys.ProductReference, cancellationToken)
            ?? new ProductReferenceSettings();
        return config.Mode == CatalogSourceMode.Off
            ? null
            : sources.FirstOrDefault(source => source.Mode == config.Mode);
    }
}
