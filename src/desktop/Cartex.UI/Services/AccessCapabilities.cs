namespace Cartex.UI.Services;

public static class AccessCapabilities
{
    public static bool FeatureEnabled(string expression) =>
        expression.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(SettingsService.Instance.IsFeatureOn);

    public static bool CanSell(Func<string, bool> hasPermission) =>
        hasPermission("sales.create|sales.checkout") && FeatureEnabled("ordering|store");

    public static bool CanQueue(Func<string, bool> hasPermission, bool allowSaleQueue) =>
        hasPermission("sales.pick|sales.view") && allowSaleQueue && FeatureEnabled("ordering|store");

    public static bool CanUseCart(Func<string, bool> hasPermission, bool allowSaleQueue) =>
        CanSell(hasPermission) || CanQueue(hasPermission, allowSaleQueue);

    public static bool CanReceiveStock(Func<string, bool> hasPermission) =>
        hasPermission("supplies.create") && FeatureEnabled("supplies");
}
