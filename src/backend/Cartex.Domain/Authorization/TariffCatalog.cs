namespace Cartex.Domain.Authorization;

public static class TariffCatalog
{
    public const string Free = "free";
    public const string Standard = "standard";
    public const string Pro = "pro";

    public static readonly IReadOnlyDictionary<string, string[]> Map = new Dictionary<string, string[]>
    {
        [Free] =
        [
            FeatureCatalog.Supplies, FeatureCatalog.Suppliers,
            FeatureCatalog.StockTransfers, FeatureCatalog.Accounts
        ],
        [Standard] =
        [
            FeatureCatalog.Supplies, FeatureCatalog.Suppliers, FeatureCatalog.StockTransfers,
            FeatureCatalog.Accounts, FeatureCatalog.Reports, FeatureCatalog.Loyalty, FeatureCatalog.Prepack,
            FeatureCatalog.OfflineCache
        ],
        [Pro] = [.. FeatureCatalog.AllCodes],
    };

    public static IReadOnlySet<string> FeaturesFor(string? tariff) =>
        (tariff is not null && Map.TryGetValue(tariff, out var codes) ? codes : Map[Free]).ToHashSet();
}
