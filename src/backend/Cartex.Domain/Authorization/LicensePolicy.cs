namespace Cartex.Domain.Authorization;

public static class LicensePolicy
{
    public static readonly IReadOnlySet<string> FreeModeAllowed = new HashSet<string>
    {
        AppPermissions.Sales.Create,
        AppPermissions.Products.View,
        AppPermissions.Stocks.View,
        AppPermissions.Customers.View,
        AppPermissions.Customers.Create,
        AppPermissions.Customers.Edit,
        AppPermissions.Customers.ReceivePayment,
        AppPermissions.Supplies.View,
        AppPermissions.Supplies.Create,
        AppPermissions.Supplies.Edit,
        AppPermissions.Supplies.Void,
        AppPermissions.Branches.ViewAll,
    };
}
