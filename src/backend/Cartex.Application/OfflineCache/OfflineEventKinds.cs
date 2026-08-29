using Cartex.Domain.Authorization;

namespace Cartex.Application.OfflineCache;

internal static class OfflineEventKinds
{
    public const string Sale = "sale.create";
    public const string Payment = "customer.payment.create";
    public const string Supply = "supply.create";

    public static string Normalize(string value) => value.Trim().ToLowerInvariant() switch
    {
        "sale" or Sale => Sale,
        "payment" or "customer.payment" or Payment => Payment,
        "supply" or Supply => Supply,
        var normalized => normalized
    };

    public static string[] ReplayActorPermissions(string kind) => kind switch
    {
        Payment => [AppPermissions.CustomerPayments.Create, AppPermissions.Customers.ReceivePayment],
        Supply => [AppPermissions.Supplies.Create],
        _ => [AppPermissions.Sales.Create, AppPermissions.Sales.Checkout]
    };
}
