namespace Cartex.Domain.Authorization;

public static class PermissionDependencies
{
    public static readonly IReadOnlyDictionary<string, string[]> Map = new Dictionary<string, string[]>
    {
        [AppPermissions.Users.View] = [AppPermissions.Roles.View, AppPermissions.Branches.View],
        [AppPermissions.Users.Manage] = [AppPermissions.Users.View],
        [AppPermissions.Roles.Manage] = [AppPermissions.Roles.View],
        [AppPermissions.Products.Toggle] = [AppPermissions.Products.View],
        [AppPermissions.Supplies.View] = [AppPermissions.Suppliers.View, AppPermissions.Warehouses.View, AppPermissions.Products.View],
        [AppPermissions.Supplies.Manage] = [AppPermissions.Supplies.View],
        [AppPermissions.Supplies.Edit] = [AppPermissions.Supplies.Manage],
        [AppPermissions.StockTransfers.View] = [AppPermissions.Warehouses.View],
        [AppPermissions.StockTransfers.Manage] = [AppPermissions.StockTransfers.View],
        [AppPermissions.Sales.Create] = [AppPermissions.Products.View, AppPermissions.Stocks.View, AppPermissions.Categories.View, AppPermissions.Customers.View],
        [AppPermissions.Sales.Return] = [AppPermissions.Sales.View],
        [AppPermissions.Shifts.ViewAll] = [AppPermissions.Shifts.View],
        [AppPermissions.Shifts.ManageAll] = [AppPermissions.Shifts.Manage, AppPermissions.Shifts.ViewAll],
    };

    public static string[] DependsOn(string permission) =>
        Map.TryGetValue(permission, out var deps) ? deps : [];

    public static IReadOnlySet<string> RequiredFor(IEnumerable<string> permissions)
    {
        var result = new HashSet<string>();
        var stack = new Stack<string>(permissions);
        while (stack.Count > 0)
            foreach (var dep in DependsOn(stack.Pop()))
                if (result.Add(dep)) stack.Push(dep);
        return result;
    }
}
