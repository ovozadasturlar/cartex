namespace Cartex.Domain.Authorization;

public static class LicensePolicy
{
    public static readonly IReadOnlySet<string> FreeModeAllowed = new HashSet<string>
    {
        AppPermissions.Sales.Create,
        AppPermissions.Products.View,
        AppPermissions.Stocks.View,
        AppPermissions.Customers.View,
        AppPermissions.Customers.Manage,
        AppPermissions.Supplies.View,
        AppPermissions.Supplies.Manage,
        AppPermissions.Branches.ViewAll,
    };
}
