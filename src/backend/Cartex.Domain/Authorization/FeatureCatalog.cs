namespace Cartex.Domain.Authorization;

public static class FeatureCatalog
{
    public const string Reports = "reports";
    public const string Loyalty = "loyalty";
    public const string StockTransfers = "stock_transfers";
    public const string Supplies = "supplies";
    public const string Suppliers = "suppliers";
    public const string Accounts = "accounts";
    public const string Multicurrency = "multicurrency";
    public const string Audit = "audit";
    public const string Ordering = "ordering";
    public const string Prepack = "prepack";
    public const string Agents = "agents";
    public const string Store = "store";
    public const string OfflineCache = "offline_cache";

    public static readonly IReadOnlyDictionary<string, string[]> Map = new Dictionary<string, string[]>
    {
        [Reports] = [AppPermissions.Reports.View, AppPermissions.Reports.Export],
        [Loyalty] = [AppPermissions.Loyalty.View, AppPermissions.Loyalty.Manage],
        [StockTransfers] = [AppPermissions.StockTransfers.View, AppPermissions.StockTransfers.Manage],
        [Supplies] = [AppPermissions.Supplies.View, AppPermissions.Supplies.Manage, AppPermissions.Supplies.Edit],
        [Suppliers] = [AppPermissions.Suppliers.View, AppPermissions.Suppliers.Manage],
        [Accounts] = [AppPermissions.Accounts.View, AppPermissions.Accounts.Manage, AppPermissions.Transactions.View],
        [Multicurrency] = [AppPermissions.Rates.Manage],
        [Audit] = [AppPermissions.Audit.View],
        [Ordering] = [],
        [Prepack] = [AppPermissions.Sales.Prepack],
        [Agents] = [],
        [Store] = [AppPermissions.Sales.Pick],
        [OfflineCache] = [],
    };

    public static readonly IReadOnlyDictionary<string, string> Names = new Dictionary<string, string>
    {
        [Reports] = "Hisobotlar",
        [Loyalty] = "Sodiqlik (cashback)",
        [StockTransfers] = "Ko'chirishlar",
        [Supplies] = "Ta'minot",
        [Suppliers] = "Yetkazib beruvchilar",
        [Accounts] = "Moliya",
        [Multicurrency] = "Ko'p valyuta",
        [Audit] = "Audit jurnali",
        [Ordering] = "Onlayn buyurtma",
        [Prepack] = "Qadoqlash (tarozi)",
        [Agents] = "Agentlar (mobil savdo)",
        [Store] = "Do'kon xodimi ilovasi",
        [OfflineCache] = "Oflayn kassa (bitta qurilma)",
    };

    public static readonly IReadOnlyList<string> AllCodes = [.. Names.Keys];

    public static readonly IReadOnlySet<string> DefaultDisabled = new HashSet<string> { Ordering, Multicurrency, Agents, Store, OfflineCache };

    public static IReadOnlySet<string> PermissionsFor(IEnumerable<string> codes) =>
        codes.SelectMany(c => Map.TryGetValue(c, out var p) ? p : []).ToHashSet();
}
