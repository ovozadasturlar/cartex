namespace Cartex.Mobile.Core;

public sealed class AppCapabilities(MobilePermissions permissions)
{
    public bool CanSell => permissions.Has("sales.create");
    public bool CanReturn => permissions.Has("sales.return");
    public bool CanOverridePrice => permissions.Has("sales.priceOverride");
    public bool CanOverrideDiscount => permissions.Has("sales.discountOverride");
    public bool CanCashOut => permissions.Has("sales.cashout");

    public bool CanViewProducts => permissions.HasAny("products.view", "sales.create");
    public bool CanManageProducts => permissions.Has("products.manage");
    public bool CanManagePrices => permissions.Has("products.manage");
    public bool CanManageCategories => permissions.Has("categories.manage");

    public bool CanViewCustomers => permissions.Has("customers.view");
    public bool CanManageCustomers => permissions.Has("customers.manage");
    public bool CanRepayDebt => permissions.Has("customers.debt");

    public bool CanViewStock => permissions.HasAny("stocks.view", "products.view");
    public bool CanManageStock => permissions.Has("stocks.manage");
    public bool CanManageSupplies => permissions.Has("supplies.manage");
    public bool CanTransfer => permissions.Has("transfers.manage");

    public bool CanManageShift => permissions.Has("shifts.manage");
    public bool CanViewReports => permissions.Has("reports.view");
    public bool CanViewOrders => permissions.HasAny("ordering.view", "sales.view");

    public bool IsManager => CanManageProducts || CanViewReports;
}
