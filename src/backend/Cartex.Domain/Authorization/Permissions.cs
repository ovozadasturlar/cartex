namespace Cartex.Domain.Authorization;

public sealed record PermissionDefinition(
    string Key,
    string Description,
    IReadOnlyList<string> DependsOn,
    bool RequiresBranch = false);

public sealed record PermissionBundleDefinition(
    string Key,
    string Description,
    IReadOnlyList<string> Permissions);

public static class AppPermissions
{
    public const string Wildcard = "*";

    public static class Branches
    {
        public const string View = "branches.view";
        public const string Create = "branches.create";
        public const string Edit = "branches.edit";
        public const string ViewAll = "branch.viewAll";
    }

    public static class Business
    {
        public const string Edit = "business.edit";
    }

    public static class Users
    {
        public const string View = "users.view";
        public const string Create = "users.create";
        public const string Edit = "users.edit";
        public const string Delete = "users.delete";
    }

    public static class Roles
    {
        public const string View = "roles.view";
        public const string Create = "roles.create";
        public const string Edit = "roles.edit";
        public const string Delete = "roles.delete";
        public const string AssignPermissions = "roles.assignPermissions";
    }

    public static class Permissions
    {
        public const string Govern = "permissions.govern";
    }

    public static class Products
    {
        public const string View = "products.view";
        public const string Create = "products.create";
        public const string Edit = "products.edit";
        public const string Delete = "products.delete";
        public const string Import = "products.import";
        public const string PrintBarcode = "products.printBarcode";
        public const string Toggle = "products.toggle";
    }

    public static class Categories
    {
        public const string View = "categories.view";
        public const string Create = "categories.create";
        public const string Edit = "categories.edit";
    }

    public static class Units
    {
        public const string View = "units.view";
        public const string Create = "units.create";
        public const string Edit = "units.edit";
        public const string Toggle = "units.toggle";
    }

    public static class ProductTypes
    {
        public const string View = "product_types.view";
        public const string Create = "product_types.create";
        public const string Edit = "product_types.edit";
    }

    public static class Manufacturers
    {
        public const string View = "manufacturers.view";
        public const string Create = "manufacturers.create";
        public const string Edit = "manufacturers.edit";
        public const string Delete = "manufacturers.delete";
    }

    public static class Barcodes
    {
        public const string Create = "barcodes.create";
        public const string Delete = "barcodes.delete";
    }

    public static class Warehouses
    {
        public const string View = "warehouses.view";
        public const string Create = "warehouses.create";
        public const string Edit = "warehouses.edit";
    }

    public static class Stocks
    {
        public const string View = "stocks.view";
        public const string Adjust = "stocks.adjust";
        public const string Reconcile = "stocks.reconcile";
        public const string WriteOff = "stocks.writeOff";
    }

    public static class StockTransfers
    {
        public const string View = "stock_transfers.view";
        public const string Create = "stock_transfers.create";
        public const string Receive = "stock_transfers.receive";
        public const string ReceiveAny = "stock_transfers.receiveAny";
    }

    public static class Sales
    {
        public const string View = "sales.view";
        public const string ViewAll = "sales.viewAll";
        public const string Create = "sales.create";
        public const string Pick = "sales.pick";
        public const string Checkout = "sales.checkout";
        public const string OverrideClaim = "sales.claim.override";
        public const string Discount = "sales.discount";
        public const string PriceOverride = "sales.priceOverride";
        public const string DiscountOverride = "sales.discountOverride";
        public const string CashOut = "sales.cashout";
        public const string Prepack = "sales.prepack";
        public const string AssignCustomer = "sales.assignCustomer";
        public const string Void = "sales.void";
    }

    public static class Currencies
    {
        public const string View = "currencies.view";
        public const string Create = "currencies.create";
        public const string Edit = "currencies.edit";
        public const string Delete = "currencies.delete";
    }

    public static class Devices
    {
        public const string View = "devices.view";
        public const string Revoke = "devices.revoke";
        public const string ViewAll = "devices.viewAll";
    }

    public static class Shifts
    {
        public const string Open = "shifts.open";
        public const string Close = "shifts.close";
        public const string View = "shifts.view";
        public const string ViewAll = "shifts.viewAll";
        public const string CloseAll = "shifts.closeAll";
    }

    public static class Supplies
    {
        public const string View = "supplies.view";
        public const string Create = "supplies.create";
        public const string Edit = "supplies.edit";
        public const string Void = "supplies.void";
        public const string Import = "supplies.import";
    }

    public static class Customers
    {
        public const string View = "customers.view";
        public const string ViewAll = "customers.viewAll";
        public const string Create = "customers.create";
        public const string Edit = "customers.edit";
        public const string Delete = "customers.delete";
        public const string ReceivePayment = "customers.receivePayment";
        public const string Refund = "customers.refund";
        public const string Loan = "customers.loan";
        public const string OpeningBalance = "customers.openingBalance";
        public const string Act = "customers.act";
        public const string Message = "customers.message";
    }

    public static class CustomerPayments
    {
        public const string View = "customer_payments.view";
        public const string Create = "customer_payments.create";
        public const string Void = "customer_payments.void";
        public const string WriteOffDebt = "customer_payments.writeOffDebt";
    }

    public static class Returns
    {
        public const string View = "returns.view";
        public const string Create = "returns.create";
        public const string FreeLine = "returns.freeLine";
        public const string Approve = "returns.approve";
        public const string Void = "returns.void";
    }

    public static class Statements
    {
        public const string View = "statements.view";
        public const string Export = "statements.export";
    }

    public static class Partners
    {
        public const string View = "partners.view";
        public const string Edit = "partners.edit";
        public const string ConfigureRoles = "partners.roles.configure";
        public const string Publish = "partners.publish";
    }

    public static class PartnerRewards
    {
        public const string View = "partner_rewards.view";
        public const string Configure = "partner_rewards.configure";
        public const string Redeem = "partner_rewards.redeem";
        public const string Adjust = "partner_rewards.adjust";
    }

    public static class Suppliers
    {
        public const string View = "suppliers.view";
        public const string Create = "suppliers.create";
        public const string Edit = "suppliers.edit";
        public const string Pay = "suppliers.pay";
    }

    public static class Accounts
    {
        public const string View = "accounts.view";
    }

    public static class Rates
    {
        public const string View = "rates.view";
        public const string Edit = "rates.edit";
    }

    public static class Notifications
    {
        public const string View = "notifications.view";
        public const string Edit = "notifications.edit";
        public const string JournalView = "notifications.journal.view";
        public const string JournalSensitive = "notifications.journal.sensitive";
        public const string JournalExport = "notifications.journal.export";
    }

    public static class Transactions
    {
        public const string View = "transactions.view";
    }

    public static class Loyalty
    {
        public const string View = "loyalty.view";
        public const string Edit = "loyalty.edit";
        public const string GrantBonus = "loyalty.grantBonus";
    }

    public static class Reports
    {
        public const string View = "reports.view";
        public const string Export = "reports.export";
    }

    public static class ExpenseCategories
    {
        public const string View = "expense_categories.view";
        public const string Create = "expense_categories.create";
        public const string Edit = "expense_categories.edit";
    }

    public static class Audit
    {
        public const string View = "audit.view";
    }

    public static class Settings
    {
        public const string Integrations = "settings.integrations";
        public const string Receipt = "settings.receipt";
        public const string BarcodeLabel = "settings.barcodeLabel";
        public const string Security = "settings.security";
        public const string SalesPolicy = "settings.salesPolicy";
    }

    public static class Printing
    {
        public const string ReceiptPrint = "printing.receipts.print";
        public const string ReceiptReprint = "printing.receipts.reprint";
        public const string BarcodePrint = "printing.barcodes.print";
        public const string ZReportPrint = "printing.z_reports.print";
        public const string DocumentPrint = "printing.documents.print";
        public const string RemoteUse = "printing.remote.use";
        public const string Host = "printing.host";
        public const string JobsViewOwn = "printing.jobs.viewOwn";
        public const string JobsViewBranch = "printing.jobs.viewBranch";
        public const string JobsRetry = "printing.jobs.retry";
        public const string JobsCancel = "printing.jobs.cancel";
        public const string NodesView = "printing.nodes.view";
        public const string NodesManage = "printing.nodes.edit";
        public const string RoutesView = "printing.routes.view";
        public const string RoutesEdit = "printing.routes.edit";
        public const string AuditView = "printing.audit.view";
        public const string AuditExport = "printing.audit.export";
    }

    public static class SmsGateway
    {
        public const string Edit = "sms.gateway.edit";
        public const string Host = "sms.gateway.host";
    }

    public static class Features
    {
        public const string View = "features.view";
        public const string Edit = "features.edit";
    }

    public static class Keys
    {
        public const string View = "keys.view";
        public const string Create = "keys.create";
        public const string Edit = "keys.edit";
        public const string Revoke = "keys.revoke";
    }

    private static PermissionDefinition P(
        string key,
        string description,
        bool branch = false,
        params string[] dependsOn) =>
        new(key, description, dependsOn, branch);

    public static readonly IReadOnlyDictionary<string, PermissionDefinition> Definitions =
        new[]
        {
            P(Branches.View, "View branches"),
            P(Branches.Create, "Create branches", false, Branches.View),
            P(Branches.Edit, "Edit branches", false, Branches.View),
            P(Branches.ViewAll, "Access data of all branches"),
            P(Business.Edit, "Edit business profile and policies"),
            P(Users.View, "View users", false, Roles.View, Branches.View),
            P(Users.Create, "Create users", false, Users.View, Roles.View, Branches.View),
            P(Users.Edit, "Edit users", false, Users.View, Roles.View, Branches.View),
            P(Users.Delete, "Delete users", false, Users.View),
            P(Roles.View, "View roles"),
            P(Roles.Create, "Create roles", false, Roles.View),
            P(Roles.Edit, "Edit roles", false, Roles.View),
            P(Roles.Delete, "Delete roles", false, Roles.View),
            P(Roles.AssignPermissions, "Assign permissions to roles", false, Roles.View),
            P(Permissions.Govern, "Enable or disable global permissions"),
            P(Products.View, "View products"),
            P(Products.Create, "Create products", false, Products.View, Categories.View, Units.View, ProductTypes.View, Manufacturers.View, Rates.View),
            P(Products.Edit, "Edit products and prices", false, Products.View, Categories.View, Units.View, ProductTypes.View, Manufacturers.View, Rates.View),
            P(Products.Delete, "Delete products", false, Products.View),
            P(Products.Import, "Import products", false, Products.Create),
            P(Products.PrintBarcode, "Generate and print product barcodes", false, Products.View),
            P(Products.Toggle, "Enable or disable products for sale", false, Products.View),
            P(Categories.View, "View categories"),
            P(Categories.Create, "Create categories", false, Categories.View),
            P(Categories.Edit, "Edit categories", false, Categories.View),
            P(Units.View, "View units"),
            P(Units.Create, "Create units", false, Units.View),
            P(Units.Edit, "Edit units", false, Units.View),
            P(Units.Toggle, "Enable or disable units", false, Units.View),
            P(ProductTypes.View, "View product types"),
            P(ProductTypes.Create, "Create product types", false, ProductTypes.View),
            P(ProductTypes.Edit, "Edit product types", false, ProductTypes.View),
            P(Manufacturers.View, "View manufacturers"),
            P(Manufacturers.Create, "Create manufacturers", false, Manufacturers.View),
            P(Manufacturers.Edit, "Edit manufacturers", false, Manufacturers.View),
            P(Manufacturers.Delete, "Delete manufacturers", false, Manufacturers.View),
            P(Barcodes.Create, "Create product barcodes", false, Products.View),
            P(Barcodes.Delete, "Delete product barcodes", false, Products.View),
            P(Warehouses.View, "View warehouses", false, Branches.View),
            P(Warehouses.Create, "Create warehouses", false, Warehouses.View, Branches.View),
            P(Warehouses.Edit, "Edit warehouses", false, Warehouses.View, Branches.View),
            P(Stocks.View, "View stock", true, Products.View, Warehouses.View, Categories.View),
            P(Stocks.Adjust, "Adjust stock", true, Stocks.View),
            P(Stocks.Reconcile, "Reconcile the stock movement journal", true, Stocks.Adjust),
            P(Stocks.WriteOff, "Write off broken, expired, lost or stolen stock", true, Stocks.Adjust),
            P(StockTransfers.View, "View stock transfers", true, Warehouses.View),
            P(StockTransfers.Create, "Create stock transfers", true, StockTransfers.View, Products.View),
            P(StockTransfers.Receive, "Receive stock transfers", true, StockTransfers.View),
            P(StockTransfers.ReceiveAny, "Receive transfers into any warehouse", true, StockTransfers.Receive),
            P(Sales.View, "View sales", true),
            P(Sales.ViewAll, "View all sales", true, Sales.View),
            P(Sales.Create, "Create sales", true, Products.View, Stocks.View, Categories.View, Customers.View, Rates.View),
            P(Sales.Pick, "Pick carts", true, Products.View, Stocks.View),
            P(Sales.Checkout, "Complete queued carts and accept payments", true, Sales.Pick, Shifts.Open, Customers.View, Rates.View),
            P(Sales.OverrideClaim, "Override another cashier's cart claim", true, Sales.Checkout),
            P(Sales.Discount, "Apply sale discount", true, Sales.Create),
            P(Sales.PriceOverride, "Override sale item price", true, Sales.Create),
            P(Sales.DiscountOverride, "Exceed discount limit", true, Sales.Discount),
            P(Sales.CashOut, "Withdraw cash from register", true, Shifts.Open, ExpenseCategories.View),
            P(Sales.Prepack, "Create or cancel prepack labels", true, Products.View, Stocks.View),
            P(Sales.Void, "Void (storno) a posted sale for correction", true, Sales.View, Returns.View),
            P(Sales.AssignCustomer, "Assign or reassign customer to completed sales", true, Sales.View, Customers.View),
            P(Currencies.View, "View currencies"),
            P(Currencies.Create, "Create currencies", false, Currencies.View),
            P(Currencies.Edit, "Edit currencies", false, Currencies.View),
            P(Currencies.Delete, "Delete currencies", false, Currencies.View),
            P(Devices.View, "View own connected devices"),
            P(Devices.Revoke, "Revoke connected devices", false, Devices.View),
            P(Devices.ViewAll, "View and revoke all users' devices", false, Devices.View),
            P(Shifts.Open, "Open cash shift", true, Shifts.View, Rates.View),
            P(Shifts.Close, "Close own cash shift", true, Shifts.Open),
            P(Shifts.View, "View own shift history", true),
            P(Shifts.ViewAll, "View all users' shifts", true, Shifts.View, Users.View),
            P(Shifts.CloseAll, "Force-close other users' shifts", true, Shifts.Close, Shifts.ViewAll),
            P(Supplies.View, "View supplies", true, Suppliers.View, Warehouses.View, Products.View),
            P(Supplies.Create, "Create supplies", true, Supplies.View, Rates.View, Units.View),
            P(Supplies.Edit, "Edit saved supplies", true, Supplies.View, Rates.View, Units.View),
            P(Supplies.Void, "Void supplies", true, Supplies.View),
            P(Supplies.Import, "Import supplies", true, Supplies.Create),
            P(Customers.View, "View customers", true),
            P(Customers.ViewAll, "View all customers", true, Customers.View),
            P(Customers.Create, "Create customers", true, Customers.View, Rates.View),
            P(Customers.Edit, "Edit customers", true, Customers.View, Rates.View),
            P(Customers.Delete, "Delete customers", true, Customers.View),
            P(Customers.ReceivePayment, "Receive customer debt payment", true, Customers.View, Rates.View),
            P(Customers.Refund, "Pay an approved customer refund", true, Customers.View, Rates.View),
            P(Customers.Loan, "Hand cash to a customer as a debt", false, Customers.Refund),
            P(Customers.OpeningBalance, "Open a customer with an existing debt or advance", true, Customers.Create),
            P(Customers.Act, "Generate a consolidated act for a customer", false, Customers.View, Sales.View),
            P(Customers.Message, "Send messages to customers", true, Customers.View),
            P(CustomerPayments.View, "View customer payment documents", true, Customers.View),
            P(CustomerPayments.Create, "Receive customer payments and advances", true, CustomerPayments.View, Customers.ReceivePayment, Rates.View),
            P(CustomerPayments.Void, "Void customer payment documents", true, CustomerPayments.View),
            P(CustomerPayments.WriteOffDebt, "Forgive customer debt", true, CustomerPayments.Create),
            P(Returns.View, "View return documents", true, Sales.View),
            P(Returns.Create, "Create product return documents", true, Returns.View, Stocks.View),
            P(Returns.FreeLine, "Return products that are not part of a recorded sale", true, Returns.Create, Products.View),
            P(Returns.Approve, "Approve return settlements and refunds", true, Returns.Create, Customers.Refund),
            P(Returns.Void, "Void return documents", true, Returns.View),
            P(Statements.View, "View customer statements", true, Customers.View),
            P(Statements.Export, "Export customer statements", true, Statements.View, Reports.Export),
            P(Partners.View, "View external business partners", true),
            P(Partners.Edit, "Create and edit external business partners", true, Partners.View),
            P(Partners.ConfigureRoles, "Configure participant roles and labels", false, Partners.View),
            P(Partners.Publish, "Record consent and publish a partner on the public page", false, Partners.Edit),
            P(PartnerRewards.View, "View partner rewards and rankings", true, Partners.View),
            P(PartnerRewards.Configure, "Configure partner reward programs", false, PartnerRewards.View, Partners.ConfigureRoles),
            P(PartnerRewards.Redeem, "Redeem or pay partner rewards", true, PartnerRewards.View),
            P(PartnerRewards.Adjust, "Adjust partner reward balances", true, PartnerRewards.View),
            P(Suppliers.View, "View suppliers", true),
            P(Suppliers.Create, "Create suppliers", true, Suppliers.View),
            P(Suppliers.Edit, "Edit suppliers", true, Suppliers.View),
            P(Suppliers.Pay, "Pay supplier debt and attach payments", true, Suppliers.View, Accounts.View, Rates.View),
            P(Accounts.View, "View accounts", true),
            P(Rates.View, "View exchange rates", false, Currencies.View),
            P(Rates.Edit, "Set exchange rates", false, Rates.View),
            P(Notifications.View, "View notification and reminder settings"),
            P(Notifications.Edit, "Edit notification and reminder settings", false, Notifications.View),
            P(Notifications.JournalView, "View notification delivery journal"),
            P(Notifications.JournalSensitive, "View notification recipients and content", false, Notifications.JournalView),
            P(Notifications.JournalExport, "Export notification delivery journal", false, Notifications.JournalView),
            P(Transactions.View, "View transactions", true),
            P(Loyalty.View, "View loyalty settings", false, Products.View, Categories.View, Manufacturers.View),
            P(Loyalty.Edit, "Edit loyalty settings", false, Loyalty.View),
            P(Loyalty.GrantBonus, "Grant customer bonus", true, Customers.View),
            P(Reports.View, "View reports", true),
            P(Reports.Export, "Export data"),
            P(ExpenseCategories.View, "View expense categories"),
            P(ExpenseCategories.Create, "Create expense categories", false, ExpenseCategories.View),
            P(ExpenseCategories.Edit, "Edit expense categories", false, ExpenseCategories.View),
            P(Audit.View, "View audit logs"),
            P(Settings.Integrations, "Manage integrations"),
            P(Settings.Receipt, "Manage receipt settings"),
            P(Settings.BarcodeLabel, "Manage barcode label content settings"),
            P(Settings.Security, "Manage login and security settings"),
            P(Settings.SalesPolicy, "Manage the sales policy"),
            P(Printing.ReceiptPrint, "Print sales receipts", true, Sales.View),
            P(Printing.ReceiptReprint, "Reprint sales receipts", true, Printing.ReceiptPrint),
            P(Printing.BarcodePrint, "Print product barcode labels", true, Products.PrintBarcode),
            P(Printing.ZReportPrint, "Print shift Z reports", true, Shifts.View),
            P(Printing.DocumentPrint, "Print business documents", true),
            P(Printing.RemoteUse, "Send print jobs to network printers", true),
            P(Printing.Host, "Run a trusted print host", true),
            P(Printing.JobsViewOwn, "View own print jobs", true),
            P(Printing.JobsViewBranch, "View branch print jobs", true, Printing.JobsViewOwn),
            P(Printing.JobsRetry, "Retry print jobs", true, Printing.JobsViewBranch),
            P(Printing.JobsCancel, "Cancel print jobs", true, Printing.JobsViewBranch),
            P(Printing.NodesView, "View printing devices", true),
            P(Printing.NodesManage, "Trust and manage printing devices", true, Printing.NodesView),
            P(Printing.RoutesView, "View printer routing", true, Printing.NodesView),
            P(Printing.RoutesEdit, "Configure printer routing", true, Printing.RoutesView, Printing.NodesManage),
            P(Printing.AuditView, "View print audit trail", true, Printing.JobsViewBranch),
            P(Printing.AuditExport, "Export print audit trail", true, Printing.AuditView),
            P(SmsGateway.Edit, "Trust and configure SMS gateway devices", true, Notifications.View),
            P(SmsGateway.Host, "Run a consented SMS gateway host", true),
            P(Features.View, "View tariff and feature state"),
            P(Features.Edit, "Edit tariff and feature state", false, Features.View),
            P(Keys.View, "View hardware login keys"),
            P(Keys.Create, "Create hardware login keys", false, Keys.View),
            P(Keys.Edit, "Enable or disable hardware login keys", false, Keys.View),
            P(Keys.Revoke, "Revoke hardware login keys", false, Keys.View),
        }.ToDictionary(x => x.Key);

    public static readonly IReadOnlyDictionary<string, string> Catalog =
        Definitions.ToDictionary(x => x.Key, x => x.Value.Description);

    public static readonly IReadOnlyList<string> DeveloperOnly =
        [Features.View, Features.Edit, Keys.View, Keys.Create, Keys.Edit, Keys.Revoke, Permissions.Govern];

    public static readonly IReadOnlyDictionary<string, PermissionBundleDefinition> Bundles =
        new[]
        {
            new PermissionBundleDefinition("supply_operator", "Kirim operatori",
                [Supplies.Create, Supplies.Edit]),
            new PermissionBundleDefinition("cashier", "Sotuvchi / kassir",
                [Sales.Create, Sales.Checkout, Sales.View, Shifts.Open, Shifts.Close, Sales.Discount,
                    CustomerPayments.Create, Returns.Create]),
            new PermissionBundleDefinition("inventory_operator", "Omborchi",
                [Stocks.View, Stocks.Adjust, StockTransfers.View, StockTransfers.Create,
                    StockTransfers.Receive, StockTransfers.ReceiveAny]),
            new PermissionBundleDefinition("catalog_editor", "Katalog muharriri",
                [Products.Create, Products.Edit, Products.Import, Categories.Create, Categories.Edit,
                    Units.Create, Units.Edit, ProductTypes.Create, ProductTypes.Edit,
                    Manufacturers.Create, Manufacturers.Edit, Barcodes.Create]),
            new PermissionBundleDefinition("customer_manager", "Mijozlar menejeri",
                [Customers.Create, Customers.Edit, Customers.Message, Customers.ReceivePayment,
                    CustomerPayments.View, CustomerPayments.Create, Returns.View,
                    Returns.Create, Returns.FreeLine, Statements.View,
                    Partners.View, Partners.Edit, PartnerRewards.View]),
            new PermissionBundleDefinition("supplier_accountant", "Ta'minotchi va to'lovlar",
                [Suppliers.Create, Suppliers.Edit, Suppliers.Pay, Supplies.View, Accounts.View, Transactions.View]),
            new PermissionBundleDefinition("accountant", "Hisobchi",
                [Accounts.View, Transactions.View, Reports.View, Reports.Export,
                    Customers.ReceivePayment, Customers.Refund, Customers.OpeningBalance,
                    CustomerPayments.View,
                    CustomerPayments.Create, CustomerPayments.Void, CustomerPayments.WriteOffDebt,
                    Returns.View, Returns.Approve,
                    Statements.View, Statements.Export,
                    Partners.View, PartnerRewards.View, PartnerRewards.Redeem,
                    Suppliers.Pay]),
            new PermissionBundleDefinition("access_administrator", "Xodimlar va ruxsatlar",
                [Users.Create, Users.Edit, Users.Delete, Roles.Create, Roles.Edit,
                    Roles.Delete, Roles.AssignPermissions]),
        }.ToDictionary(x => x.Key);

    public static readonly IReadOnlyDictionary<string, string[]> LegacyReplacements =
        new Dictionary<string, string[]>
        {
            ["sales.return"] = [Returns.View, Returns.Create],
            ["branches.manage"] = [Branches.Create, Branches.Edit],
            ["business.manage"] = [Business.Edit, ExpenseCategories.View, ExpenseCategories.Create, ExpenseCategories.Edit],
            ["users.manage"] = [Users.Create, Users.Edit, Users.Delete],
            ["roles.manage"] = [Roles.Create, Roles.Edit, Roles.Delete, Roles.AssignPermissions],
            ["products.manage"] =
            [
                Products.Create, Products.Edit, Products.Delete, Products.Import,
                Units.View, Units.Create, Units.Edit, Units.Toggle,
                ProductTypes.View, ProductTypes.Create, ProductTypes.Edit,
                Manufacturers.View, Manufacturers.Create, Manufacturers.Edit, Manufacturers.Delete,
                Barcodes.Create, Barcodes.Delete
            ],
            ["sales.manage"] =
            [
                Sales.Create, Sales.Pick, Sales.Checkout, Sales.OverrideClaim,
                Sales.Discount, Sales.PriceOverride, Sales.DiscountOverride,
                Sales.CashOut, Sales.Prepack
            ],
            ["categories.manage"] = [Categories.Create, Categories.Edit],
            ["warehouses.manage"] = [Warehouses.Create, Warehouses.Edit],
            ["stocks.manage"] = [Stocks.Adjust],
            ["stock_transfers.manage"] = [StockTransfers.Create, StockTransfers.Receive],
            ["currencies.manage"] = [Currencies.View, Currencies.Create, Currencies.Edit, Currencies.Delete],
            ["devices.manage"] = [Devices.View, Devices.Revoke],
            ["shifts.manage"] = [Shifts.Open, Shifts.Close],
            ["shifts.manageAll"] = [Shifts.CloseAll],
            ["supplies.manage"] = [Supplies.Create, Supplies.Edit, Supplies.Void, Supplies.Import],
            ["customers.manage"] = [Customers.Create, Customers.Edit, Customers.Delete, Customers.ReceivePayment,
                Customers.Refund, CustomerPayments.View, CustomerPayments.Create, CustomerPayments.Void,
                Returns.View, Returns.Create, Returns.FreeLine, Returns.Approve, Returns.Void,
                Statements.View, Statements.Export],
            ["partners.manage"] = [Partners.View, Partners.Edit, Partners.ConfigureRoles,
                PartnerRewards.View, PartnerRewards.Configure, PartnerRewards.Redeem, PartnerRewards.Adjust],
            ["suppliers.manage"] = [Suppliers.Create, Suppliers.Edit, Suppliers.Pay],
            ["accounts.manage"] = [Accounts.View],
            ["rates.manage"] = [Rates.View, Rates.Edit],
            ["notifications.manage"] = [Notifications.View, Notifications.Edit],
            ["loyalty.manage"] = [Loyalty.Edit, Loyalty.GrantBonus],
            ["features.manage"] = [Features.View, Features.Edit],
            ["keys.manage"] = [Keys.View, Keys.Create, Keys.Edit, Keys.Revoke],
            ["settings.manage"] = [Settings.Integrations, Settings.Receipt, Settings.BarcodeLabel, Settings.Security, Settings.SalesPolicy],
        };
}
