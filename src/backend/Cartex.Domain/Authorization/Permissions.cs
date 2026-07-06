namespace Cartex.Domain.Authorization;

public static class AppPermissions
{
    public const string Wildcard = "*";

    public static class Branches
    {
        public const string View = "branches.view";
        public const string Manage = "branches.manage";
        public const string ViewAll = "branch.viewAll";
    }

    public static class Business
    {
        public const string Manage = "business.manage";
    }

    public static class Users
    {
        public const string View = "users.view";
        public const string Manage = "users.manage";
    }

    public static class Roles
    {
        public const string View = "roles.view";
        public const string Manage = "roles.manage";
    }

    public static class Permissions
    {
        public const string Govern = "permissions.govern";
    }

    public static class Products
    {
        public const string View = "products.view";
        public const string Manage = "products.manage";
        public const string PrintBarcode = "products.printBarcode";
    }

    public static class Categories
    {
        public const string View = "categories.view";
        public const string Manage = "categories.manage";
    }

    public static class Warehouses
    {
        public const string View = "warehouses.view";
        public const string Manage = "warehouses.manage";
    }

    public static class Stocks
    {
        public const string View = "stocks.view";
        public const string Manage = "stocks.manage";
    }

    public static class StockTransfers
    {
        public const string View = "stock_transfers.view";
        public const string Manage = "stock_transfers.manage";
    }

    public static class Sales
    {
        public const string View = "sales.view";
        public const string Create = "sales.create";
        public const string Return = "sales.return";
        public const string Discount = "sales.discount";
        public const string PriceOverride = "sales.priceOverride";
        public const string CashOut = "sales.cashout";
    }

    public static class Shifts
    {
        public const string Manage = "shifts.manage";
        public const string View = "shifts.view";
    }

    public static class Supplies
    {
        public const string View = "supplies.view";
        public const string Manage = "supplies.manage";
    }

    public static class Customers
    {
        public const string View = "customers.view";
        public const string Manage = "customers.manage";
    }

    public static class Suppliers
    {
        public const string View = "suppliers.view";
        public const string Manage = "suppliers.manage";
    }

    public static class Accounts
    {
        public const string View = "accounts.view";
        public const string Manage = "accounts.manage";
    }

    public static class Rates
    {
        public const string Manage = "rates.manage";
    }

    public static class Notifications
    {
        public const string Manage = "notifications.manage";
    }

    public static class Transactions
    {
        public const string View = "transactions.view";
    }

    public static class Loyalty
    {
        public const string View = "loyalty.view";
        public const string Manage = "loyalty.manage";
    }

    public static class Reports
    {
        public const string View = "reports.view";
        public const string Export = "reports.export";
    }

    public static class Audit
    {
        public const string View = "audit.view";
    }

    public static class Settings
    {
        public const string Manage = "settings.manage";
    }

    public static class Features
    {
        public const string Manage = "features.manage";
    }

    public static class Keys
    {
        public const string Manage = "keys.manage";
    }

    public static readonly IReadOnlyList<string> DeveloperOnly =
        [Settings.Manage, Features.Manage, Keys.Manage, Permissions.Govern];

    public static readonly IReadOnlyDictionary<string, string> Catalog = new Dictionary<string, string>
    {
        [Branches.View] = "View branches",
        [Branches.Manage] = "Create/edit branches",
        [Branches.ViewAll] = "Access data of all branches",
        [Users.View] = "View users",
        [Users.Manage] = "Create/edit users",
        [Roles.View] = "View roles",
        [Roles.Manage] = "Create/edit roles and assign permissions",
        [Products.View] = "View products",
        [Products.Manage] = "Create/edit products",
        [Products.PrintBarcode] = "Generate/print product barcodes",
        [Categories.View] = "View categories",
        [Categories.Manage] = "Create/edit categories",
        [Warehouses.View] = "View warehouses",
        [Warehouses.Manage] = "Create/edit warehouses",
        [Stocks.View] = "View stock",
        [Stocks.Manage] = "Manage stock",
        [StockTransfers.View] = "View transfers",
        [StockTransfers.Manage] = "Create/manage transfers",
        [Sales.View] = "View sales",
        [Sales.Create] = "Create sales (POS)",
        [Sales.Return] = "Return sales",
        [Sales.Discount] = "Apply discount on sale",
        [Sales.PriceOverride] = "Override item price during sale",
        [Sales.CashOut] = "Cash withdrawal/expense from register",
        [Shifts.Manage] = "Open/close cash shift",
        [Shifts.View] = "View shift history",
        [Supplies.View] = "View supplies",
        [Supplies.Manage] = "Create/edit supplies",
        [Customers.View] = "View customers",
        [Customers.Manage] = "Create/edit customers",
        [Suppliers.View] = "View suppliers",
        [Suppliers.Manage] = "Create/edit suppliers",
        [Accounts.View] = "View accounts",
        [Accounts.Manage] = "Manage accounts",
        [Rates.Manage] = "Manage exchange rates",
        [Notifications.Manage] = "Manage notifications and reminders",
        [Transactions.View] = "View transactions",
        [Loyalty.View] = "View loyalty/cashback settings",
        [Loyalty.Manage] = "Manage loyalty/cashback settings",
        [Reports.View] = "View reports",
        [Reports.Export] = "Export data (Excel/PDF/CSV)",
        [Audit.View] = "View audit logs",
        [Business.Manage] = "Manage business profile and onboarding",
        [Settings.Manage] = "Manage business settings (developer)",
        [Features.Manage] = "Manage tariff and features (developer)",
        [Keys.Manage] = "Generate hardware login keys (developer)",
        [Permissions.Govern] = "Enable/disable global permissions (developer)",
    };
}
