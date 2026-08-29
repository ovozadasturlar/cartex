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
    public const string PricingMulticurrency = "multicurrency_pricing";
    public const string SalesMulticurrency = "multicurrency_sales";
    public const string Audit = "audit";
    public const string Ordering = "ordering";
    public const string Prepack = "prepack";
    public const string Agents = "agents";
    public const string Store = "store";
    public const string OfflineCache = "offline_cache";
    public const string RemotePrinting = "remote_printing";
    public const string Partners = "partners";

    public static readonly IReadOnlyDictionary<string, string[]> Map = new Dictionary<string, string[]>
    {
        [Reports] = [AppPermissions.Reports.View, AppPermissions.Reports.Export],
        [Loyalty] = [AppPermissions.Loyalty.View, AppPermissions.Loyalty.Edit, AppPermissions.Loyalty.GrantBonus],
        [StockTransfers] = [AppPermissions.StockTransfers.View, AppPermissions.StockTransfers.Create, AppPermissions.StockTransfers.Receive],
        [Supplies] = [AppPermissions.Supplies.View, AppPermissions.Supplies.Create, AppPermissions.Supplies.Edit, AppPermissions.Supplies.Void, AppPermissions.Supplies.Import],
        [Suppliers] = [AppPermissions.Suppliers.View, AppPermissions.Suppliers.Create, AppPermissions.Suppliers.Edit, AppPermissions.Suppliers.Pay],
        [Accounts] = [AppPermissions.Accounts.View, AppPermissions.Transactions.View],
        [Multicurrency] = [AppPermissions.Rates.View, AppPermissions.Rates.Edit, AppPermissions.Currencies.View,
            AppPermissions.Currencies.Create, AppPermissions.Currencies.Edit, AppPermissions.Currencies.Delete],
        [PricingMulticurrency] = [],
        [SalesMulticurrency] = [],
        [Audit] = [AppPermissions.Audit.View],
        [Ordering] = [],
        [Prepack] = [AppPermissions.Sales.Prepack],
        [Agents] = [],
        [Store] = [AppPermissions.Sales.Pick],
        [OfflineCache] = [],
        [RemotePrinting] = [AppPermissions.Printing.RemoteUse,
            AppPermissions.Printing.Host, AppPermissions.Printing.JobsViewOwn,
            AppPermissions.Printing.JobsViewBranch, AppPermissions.Printing.JobsRetry,
            AppPermissions.Printing.JobsCancel, AppPermissions.Printing.NodesView,
            AppPermissions.Printing.NodesManage, AppPermissions.Printing.RoutesView,
            AppPermissions.Printing.RoutesEdit, AppPermissions.Printing.AuditView,
            AppPermissions.Printing.AuditExport],
        [Partners] = [AppPermissions.Partners.View, AppPermissions.Partners.Edit,
            AppPermissions.Partners.ConfigureRoles, AppPermissions.PartnerRewards.View,
            AppPermissions.PartnerRewards.Configure, AppPermissions.PartnerRewards.Redeem,
            AppPermissions.PartnerRewards.Adjust],
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
        [PricingMulticurrency] = "Narxlash va kirim uchun ko'p valyuta",
        [SalesMulticurrency] = "Savdo va to'lov uchun ko'p valyuta",
        [Audit] = "Audit jurnali",
        [Ordering] = "Onlayn buyurtma",
        [Prepack] = "Qadoqlash (tarozi)",
        [Agents] = "Agentlar (mobil savdo)",
        [Store] = "Do'kon xodimi ilovasi",
        [OfflineCache] = "Oflayn kassa (bitta qurilma)",
        [RemotePrinting] = "Tarmoq orqali chop etish",
        [Partners] = "Hamkorlar (usta) sodiqligi",
    };

    public static readonly IReadOnlyList<string> AllCodes = [.. Names.Keys];

    public static readonly IReadOnlySet<string> DefaultDisabled = new HashSet<string>
    {
        Ordering, Multicurrency, PricingMulticurrency, SalesMulticurrency, Agents, Store, OfflineCache, RemotePrinting
    };

    public static readonly IReadOnlyList<string> ConfigurableCodes = [.. AllCodes.Where(code => code != Multicurrency)];

    public static IReadOnlySet<string> Normalize(IEnumerable<string> codes)
    {
        var normalized = codes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var hasPricing = normalized.Contains(PricingMulticurrency);
        var hasSales = normalized.Contains(SalesMulticurrency);

        if (normalized.Contains(Multicurrency) && !hasPricing && !hasSales)
        {
            normalized.Add(PricingMulticurrency);
            normalized.Add(SalesMulticurrency);
        }

        if (normalized.Contains(PricingMulticurrency) || normalized.Contains(SalesMulticurrency))
            normalized.Add(Multicurrency);

        return normalized;
    }

    public static IReadOnlySet<string> PermissionsFor(IEnumerable<string> codes) =>
        codes.SelectMany(c => Map.TryGetValue(c, out var p) ? p : []).ToHashSet();
}
