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
    }
}
