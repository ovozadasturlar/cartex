using Cartex.Mobile.Agent.Views;

namespace Cartex.Mobile.Agent;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute("customer", typeof(CustomerPage));
        Routing.RegisterRoute("sale", typeof(SalePage));
        Routing.RegisterRoute("repay", typeof(RepayPage));
        Routing.RegisterRoute("scan", typeof(ScanPage));
        Routing.RegisterRoute("order", typeof(OrderPage));
        Routing.RegisterRoute("outbox", typeof(OutboxPage));
        Routing.RegisterRoute("change-password", typeof(ChangePasswordPage));
        Routing.RegisterRoute("devices", typeof(DevicesPage));
        Routing.RegisterRoute("customer-new", typeof(CustomerCreatePage));
        Routing.RegisterRoute("vanstock", typeof(VanStockPage));
        Routing.RegisterRoute("pin", typeof(PinPage));
    }
}
