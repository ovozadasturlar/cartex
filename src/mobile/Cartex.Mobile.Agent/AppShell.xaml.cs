using Cartex.Mobile.Agent.Views;

namespace Cartex.Mobile.Agent;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute("home", typeof(HomePage));
        Routing.RegisterRoute("vanstock", typeof(VanStockPage));
        Routing.RegisterRoute("transfers", typeof(TransfersPage));
        Routing.RegisterRoute("product", typeof(ProductPage));
        Routing.RegisterRoute("product-edit", typeof(ProductEditPage));
        Routing.RegisterRoute("customer", typeof(CustomerPage));
        Routing.RegisterRoute("sale", typeof(SalePage));
        Routing.RegisterRoute("repay", typeof(RepayPage));
        Routing.RegisterRoute("scan", typeof(ScanPage));
        Routing.RegisterRoute("order", typeof(OrderPage));
        Routing.RegisterRoute("outbox", typeof(OutboxPage));
        Routing.RegisterRoute("change-password", typeof(ChangePasswordPage));
        Routing.RegisterRoute("devices", typeof(DevicesPage));
        Routing.RegisterRoute("customer-new", typeof(CustomerCreatePage));
        Routing.RegisterRoute("map-picker", typeof(MapPickerPage));
        Routing.RegisterRoute("customers-map", typeof(CustomersMapPage));
        Routing.RegisterRoute("visit-route", typeof(RoutePage));
        Routing.RegisterRoute("day-summary", typeof(DaySummaryPage));
        Routing.RegisterRoute("pin", typeof(PinPage));
        Routing.RegisterRoute("security", typeof(SecurityPage));
    }

    protected override void OnNavigated(ShellNavigatedEventArgs args)
    {
        base.OnNavigated(args);
        if (args.Source is ShellNavigationSource.ShellSectionChanged or ShellNavigationSource.ShellItemChanged or ShellNavigationSource.ShellContentChanged
            && Navigation.NavigationStack.Count > 1)
            Dispatcher.Dispatch(async () => await Navigation.PopToRootAsync(false));
    }
}
