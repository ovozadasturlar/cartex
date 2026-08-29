using Cartex.Mobile.Agent.Views;
using Cartex.Mobile.Core;

namespace Cartex.Mobile.Agent;

public partial class AppShell : Shell
{
    private readonly AccessState _access;

    public AppShell()
    {
        InitializeComponent();
        _access = IPlatformApplication.Current!.Services.GetRequiredService<AccessState>();
        _access.Changed += RefreshAccess;
        RefreshAccess();
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
        Routing.RegisterRoute("server-scan", typeof(ServerScanPage));
    }

    private async void RefreshAccess() => await Dispatcher.DispatchAsync(() =>
    {
        CatalogTab.IsVisible = _access.CanAgentViewCatalog;
        CartTab.IsVisible = _access.CanAgentUseCart;
        CustomersTab.IsVisible = _access.CanAgentViewCustomers;
        OrdersTab.IsVisible = _access.CanAgentViewOrders;
    });

    protected override void OnNavigated(ShellNavigatedEventArgs args)
    {
        base.OnNavigated(args);
        if (args.Source is ShellNavigationSource.ShellSectionChanged or ShellNavigationSource.ShellItemChanged or ShellNavigationSource.ShellContentChanged
            && Navigation.NavigationStack.Count > 1)
            Dispatcher.Dispatch(() => _ = PopToRootAsync());
    }

    private async Task PopToRootAsync()
    {
        try
        {
            await Navigation.PopToRootAsync(false);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(exception);
        }
    }
}
