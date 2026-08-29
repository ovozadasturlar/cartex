using System.Reflection;
using Cartex.Auth.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Cartex.ArchitectureTests;

public class EndpointAuthorizationTests
{
    private static readonly HashSet<string> AnonymousWhitelist =
    [
        "AuthController.Login",
        "AuthController.LoginWithKey",
        "AuthController.StartQrLogin",
        "AuthController.PollQrLogin",
        "AuthController.LoginMethods",
        "AuthController.Refresh",
        "AuthController.Logout",
        "StorageController.GetContent",
        "ReceiptController.GetReceipt",
        "ReceiptController.GetReceiptPdf",
        "ReceiptController.GetReceiptPrintPages",
        "StoreAuthController.RequestOtp",
        "StoreAuthController.Verify",
        "StoreAuthController.Telegram",
        "StoreAuthController.Refresh",
        "StoreAuthController.Logout",
        "NotificationsController.PlayMobileStatus",
    ];

    private static readonly HashSet<string> AuthenticatedOnlyWhitelist =
    [
        "AuthController.ApproveQrLogin",
        "AuthController.ChangePassword",
        "AuthController.Context",
        "BusinessController.Get",
        "RatesController.GetCurrencies",
        "ExpenseCategoriesController.GetExpenseCategories",
        "FeaturesController.GetEnabled",
        "SettingsController.GetReceipt",
        "SettingsController.GetProforma",
        "SettingsController.GetSalesPolicy",
        "StoreController.Me",
        "StoreController.SetLanguage",
        "StoreController.Info",
        "StoreController.Catalog",
        "StoreController.SubmitCart",
        "StoreController.Orders",
        "StoreController.Receipts",
        "StoreController.Balance",
    ];

    private static IEnumerable<MethodInfo> ControllerActions() =>
        typeof(Program).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(ControllerBase)) && !t.IsAbstract)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m => !m.IsSpecialName);

    private static string Id(MethodInfo m) => $"{m.DeclaringType!.Name}.{m.Name}";

    private static bool IsAnonymous(MethodInfo m) =>
        m.GetCustomAttribute<AllowAnonymousAttribute>() is not null ||
        m.DeclaringType!.GetCustomAttribute<AllowAnonymousAttribute>() is not null;

    [Fact]
    public void Every_action_has_permission_or_is_whitelisted()
    {
        var violations = ControllerActions()
            .Where(m => m.GetCustomAttribute<HasPermissionAttribute>() is null)
            .Select(Id)
            .Where(id => !AnonymousWhitelist.Contains(id) && !AuthenticatedOnlyWhitelist.Contains(id))
            .ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void AllowAnonymous_only_on_whitelisted_actions()
    {
        var violations = ControllerActions()
            .Where(IsAnonymous)
            .Select(Id)
            .Where(id => !AnonymousWhitelist.Contains(id))
            .ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Whitelists_have_no_stale_entries()
    {
        var actions = ControllerActions().Select(Id).ToHashSet();
        var stale = AnonymousWhitelist.Concat(AuthenticatedOnlyWhitelist)
            .Where(id => !actions.Contains(id))
            .ToList();

        Assert.Empty(stale);
    }
}
