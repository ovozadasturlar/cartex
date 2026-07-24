using Cartex.Mobile.Store.Views;

namespace Cartex.Mobile.Store;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute("cart", typeof(CartPage));
        Routing.RegisterRoute("handoff", typeof(HandoffPage));
        Routing.RegisterRoute("checkout", typeof(CheckoutPage));
        Routing.RegisterRoute("receive_cart", typeof(ReceiveCartPage));
        Routing.RegisterRoute("product/edit", typeof(ProductEditPage));
        Routing.RegisterRoute("barcode_attach", typeof(BarcodeAttachPage));
        Routing.RegisterRoute("change-password", typeof(ChangePasswordPage));
        Routing.RegisterRoute("devices", typeof(DevicesPage));
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
