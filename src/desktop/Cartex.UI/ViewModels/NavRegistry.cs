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
        new("main", "pos", MaterialIconKind.CashRegister, typeof(SalesViewModel), "sales.create|sales.checkout"),
        new("sales", "shift", MaterialIconKind.CashClock, typeof(ShiftViewModel), "shifts.view"),
        new("sales", "sale_history", MaterialIconKind.ChartLine, typeof(SalesHistoryViewModel), "sales.view"),
        new("sales", "orders", MaterialIconKind.ClipboardTextClockOutline, typeof(OrdersViewModel), "sales.view", "ordering"),
        new("sales", "returns", MaterialIconKind.KeyboardReturn, typeof(ReturnsViewModel), "returns.view"),
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
        new("catalog", "categories", MaterialIconKind.ShapeOutline, typeof(CategoriesViewModel), "categories.view"),
        new("catalog", "units", MaterialIconKind.RulerSquare, typeof(UnitsViewModel), "units.view"),
        new("catalog", "product_types", MaterialIconKind.TagOutline, typeof(ProductTypesViewModel), "product_types.view"),
        new("catalog", "manufacturers", MaterialIconKind.Factory, typeof(ManufacturersViewModel), "manufacturers.view"),
        new("organization", "business", MaterialIconKind.Domain, typeof(BusinessSettingsViewModel), "business.edit"),
        new("organization", "sales_policy", MaterialIconKind.ScaleBalance, typeof(SalesPolicyViewModel), "settings.salesPolicy"),
        new("organization", "modules", MaterialIconKind.ToggleSwitchOutline, typeof(ModulesViewModel), "business.edit"),
        new("organization", "branch", MaterialIconKind.OfficeBuildingOutline, typeof(BranchesViewModel), "branches.view"),
        new("organization", "warehouse", MaterialIconKind.Warehouse, typeof(WarehousesViewModel), "warehouses.view"),
        new("organization", "suppliers", MaterialIconKind.TruckOutline, typeof(SuppliersViewModel), "suppliers.view"),
        new("access", "users", MaterialIconKind.AccountCog, typeof(UsersViewModel), "users.view"),
        new("access", "roles", MaterialIconKind.ShieldAccount, typeof(RolesViewModel), "roles.view"),
        new("access", "permissions_matrix", MaterialIconKind.ShieldKeyOutline, typeof(PermissionsMatrixViewModel), "roles.assignPermissions"),
        new("system", "loyalty", MaterialIconKind.GiftOutline, typeof(LoyaltyViewModel), "loyalty.view"),
        new("system", "expense_categories", MaterialIconKind.CashMinus, typeof(ExpenseCategoriesViewModel), "expense_categories.view"),
        new("system", "audit", MaterialIconKind.History, typeof(AuditViewModel), "audit.view"),
        new("system", "rates", MaterialIconKind.CurrencyUsd, typeof(RatesViewModel), "rates.view"),
        new("system", "reminders", MaterialIconKind.BellRingOutline, typeof(RemindersViewModel), "notifications.view"),
        new("system", "notification_journal", MaterialIconKind.MessageBadgeOutline, typeof(NotificationJournalViewModel), "notifications.journal.view"),
        new("system", "printing", MaterialIconKind.Printer, typeof(PrintingViewModel), "settings.receipt|settings.barcodeLabel|printing.routes.view|printing.jobs.viewOwn|printing.jobs.viewBranch"),
        new("system", "devices", MaterialIconKind.Devices, typeof(DevicesViewModel), "devices.view"),
        new("system", "app_settings", MaterialIconKind.Cog, typeof(SettingsViewModel), null),
        new("developer", "tariff_features", MaterialIconKind.KeyVariant, typeof(TariffFeaturesViewModel), "features.view"),
        new("developer", "integrations", MaterialIconKind.LinkVariant, typeof(IntegrationsViewModel), "settings.integrations"),
        new("developer", "hardware_keys", MaterialIconKind.Usb, typeof(HardwareKeysViewModel), "keys.view"),
    ];
}
