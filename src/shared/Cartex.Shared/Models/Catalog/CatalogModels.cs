namespace Cartex.Shared.Models.Catalog;

public enum CatalogSourceMode
{
    Off,
    Online,
    File
}

public sealed record CatalogProductDto(
    string Barcode,
    string Name,
    string? NameCyrl,
    string? Manufacturer,
    string? CategoryParent,
    string? CategoryChild,
    string? Model,
    string? Unit,
    decimal? PackQty,
    string? ImageUrl);

public sealed record CatalogPackDto(string ShopType, int Version, int RowCount, DateTime UploadedAt);

public sealed record CatalogSettingsDto
{
    public CatalogSourceMode Mode { get; init; } = CatalogSourceMode.Online;
    public string EndpointBaseUrl { get; init; } = string.Empty;
    public string ImageBaseUrl { get; init; } = string.Empty;
    public CatalogPackDto? Pack { get; init; }
    public string? LastError { get; init; }
}
