namespace Cartex.Mobile.Core;

public sealed class AppCapabilities(MobilePermissions permissions)
{
    public bool CanSell => permissions.Has("sales.create");
    public bool CanCheckout => permissions.Has("sales.checkout");
    public bool CanReturn => permissions.Has("returns.create");
    public bool CanOverridePrice => permissions.Has("sales.priceOverride");
    public bool CanOverrideDiscount => permissions.Has("sales.discountOverride");
    public bool CanCashOut => permissions.Has("sales.cashout");

    public bool CanViewProducts => permissions.HasAny("products.view", "sales.create", "sales.checkout");
    public bool CanManageProducts => permissions.Has("products.edit");
    public bool CanManagePrices => permissions.Has("products.edit");
    public bool CanManageCategories => permissions.HasAny("categories.create", "categories.edit");

    public bool CanViewCustomers => permissions.Has("customers.view");
    public bool CanManageCustomers => permissions.HasAny("customers.create", "customers.edit");
    public bool CanRepayDebt => permissions.Has("customers.receivePayment");

    public bool CanViewStock => permissions.HasAny("stocks.view", "products.view");
    public bool CanManageStock => permissions.Has("stocks.adjust");
    public bool CanManageSupplies => permissions.HasAny("supplies.create", "supplies.edit");
    public bool CanTransfer => permissions.HasAny("stock_transfers.create", "stock_transfers.receive");

    public bool CanManageShift => permissions.HasAny("shifts.open", "shifts.close");
    public bool CanViewReports => permissions.Has("reports.view");
    public bool CanViewOrders => permissions.HasAny("ordering.view", "sales.view");

    public bool IsManager => CanManageProducts || CanViewReports;
}
