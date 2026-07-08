using Material.Icons;

namespace Cartex.UI.ViewModels;

public static class NavRegistry
{
    public record NavDef(string SectionKey, string Key, MaterialIconKind Icon, Type VmType, string? Permission, string? Feature = null);

    public static readonly (string Key, string TitleKey)[] SidebarSections =
    [
        ("main", "section_main"),
        ("sales", "section_sales"),
        ("inventory", "section_inventory"),
        ("finance", "section_finance"),
    ];

    public static readonly NavDef[] SidebarPages =
    [
        new("main", "dashboard", MaterialIconKind.ViewDashboard, typeof(DashboardViewModel), "reports.view"),
        new("main", "pos", MaterialIconKind.CashRegister, typeof(SalesViewModel), "sales.create"),
        new("sales", "shift", MaterialIconKind.CashClock, typeof(ShiftViewModel), "sales.create"),
        new("sales", "sale_history", MaterialIconKind.ChartLine, typeof(SalesHistoryViewModel), "sales.view"),
        new("sales", "orders", MaterialIconKind.ClipboardTextClockOutline, typeof(OrdersViewModel), "sales.view", "ordering"),
        new("sales", "customers", MaterialIconKind.AccountGroup, typeof(CustomersViewModel), "customers.view"),
        new("inventory", "products", MaterialIconKind.PackageVariantClosed, typeof(ProductsViewModel), "products.view"),
        new("inventory", "inventory", MaterialIconKind.Warehouse, typeof(WarehouseViewModel), "stocks.view"),
        new("inventory", "supplies", MaterialIconKind.TruckCheckOutline, typeof(SuppliesViewModel), "supplies.view"),
        new("inventory", "barcode_print", MaterialIconKind.BarcodeScan, typeof(BarcodePrintViewModel), "products.printBarcode"),
        new("inventory", "transfers", MaterialIconKind.SwapHorizontal, typeof(TransfersViewModel), "stock_transfers.view"),
        new("finance", "accounts", MaterialIconKind.WalletOutline, typeof(AccountsViewModel), "accounts.view"),
        new("finance", "transactions", MaterialIconKind.SwapHorizontal, typeof(TransactionsViewModel), "transactions.view"),
        new("finance", "reports", MaterialIconKind.ChartBar, typeof(ReportsViewModel), "reports.view"),
    ];

    public static readonly (string Key, string TitleKey)[] SettingsSections =
    [
        ("catalog", "section_catalog"),
        ("organization", "settings_org"),
        ("access", "settings_access"),
        ("system", "settings_system"),
        ("developer", "settings_developer"),
    ];

    public static readonly NavDef[] SettingsPages =
    [
        new("catalog", "categories", MaterialIconKind.ShapeOutline, typeof(CategoriesViewModel), "categories.manage"),
        new("catalog", "units", MaterialIconKind.RulerSquare, typeof(UnitsViewModel), "products.manage"),
        new("catalog", "product_types", MaterialIconKind.TagOutline, typeof(ProductTypesViewModel), "products.manage"),
        new("organization", "business", MaterialIconKind.Domain, typeof(BusinessSettingsViewModel), "business.manage"),
        new("organization", "branch", MaterialIconKind.OfficeBuildingOutline, typeof(BranchesViewModel), "branches.manage"),
        new("organization", "warehouse", MaterialIconKind.Warehouse, typeof(WarehousesViewModel), "warehouses.manage"),
        new("organization", "suppliers", MaterialIconKind.TruckOutline, typeof(SuppliersViewModel), "suppliers.manage"),
        new("access", "users", MaterialIconKind.AccountCog, typeof(UsersViewModel), "users.view"),
        new("access", "roles", MaterialIconKind.ShieldAccount, typeof(RolesViewModel), "roles.view"),
        new("access", "permissions_matrix", MaterialIconKind.ShieldKeyOutline, typeof(PermissionsMatrixViewModel), "roles.manage"),
        new("system", "loyalty", MaterialIconKind.GiftOutline, typeof(LoyaltyViewModel), "loyalty.manage"),
        new("system", "expense_categories", MaterialIconKind.CashMinus, typeof(ExpenseCategoriesViewModel), "settings.manage"),
        new("system", "audit", MaterialIconKind.History, typeof(AuditViewModel), "audit.view"),
        new("system", "rates", MaterialIconKind.CurrencyUsd, typeof(RatesViewModel), "rates.manage"),
        new("system", "reminders", MaterialIconKind.BellRingOutline, typeof(RemindersViewModel), "notifications.manage"),
        new("system", "receipt_settings", MaterialIconKind.ReceiptTextOutline, typeof(ReceiptSettingsViewModel), "business.manage"),
        new("system", "printing", MaterialIconKind.Printer, typeof(PrintingViewModel), "business.manage"),
        new("system", "devices", MaterialIconKind.Devices, typeof(DevicesViewModel), "devices.manage"),
        new("system", "app_settings", MaterialIconKind.Cog, typeof(SettingsViewModel), null),
        new("developer", "tariff_features", MaterialIconKind.KeyVariant, typeof(TariffFeaturesViewModel), "settings.manage"),
        new("developer", "integrations", MaterialIconKind.LinkVariant, typeof(IntegrationsViewModel), "settings.manage"),
        new("developer", "hardware_keys", MaterialIconKind.Usb, typeof(HardwareKeysViewModel), "keys.manage"),
    ];
}
