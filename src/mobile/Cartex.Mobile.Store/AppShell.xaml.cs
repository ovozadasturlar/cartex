using Cartex.Mobile.Store.Views;
using Cartex.Mobile.Store.Services;

namespace Cartex.Mobile.Store;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute("cart", typeof(CartPage));
        Routing.RegisterRoute("handoff", typeof(HandoffPage));
        Routing.RegisterRoute("sale/detail", typeof(SaleDetailPage));
        Routing.RegisterRoute("cart/edit", typeof(CartEditPage));
        Routing.RegisterRoute("customer/detail", typeof(CustomerDetailPage));
        Routing.RegisterRoute("customer/statement", typeof(CustomerStatementPage));
        Routing.RegisterRoute("customer/refund", typeof(CustomerRefundPage));
        Routing.RegisterRoute("sale/return", typeof(SaleReturnPage));
        Routing.RegisterRoute("case/detail", typeof(TradeCaseDetailPage));
        Routing.RegisterRoute("case/issue", typeof(CaseIssuePage));
        Routing.RegisterRoute("case/return", typeof(CaseReturnPage));
        Routing.RegisterRoute("case/payment", typeof(CasePaymentPage));
        Routing.RegisterRoute("case/statement", typeof(CaseStatementPage));
        Routing.RegisterRoute("case/settle", typeof(CaseSettlementPage));
        Routing.RegisterRoute("case/create", typeof(CaseCreatePage));
        Routing.RegisterRoute("checkout", typeof(CheckoutPage));
        Routing.RegisterRoute("receive_cart", typeof(ReceiveCartPage));
        Routing.RegisterRoute("product/edit", typeof(ProductEditPage));
        Routing.RegisterRoute("barcode_attach", typeof(BarcodeAttachPage));
        Routing.RegisterRoute("change-password", typeof(ChangePasswordPage));
        Routing.RegisterRoute("devices", typeof(DevicesPage));
        Routing.RegisterRoute("pin", typeof(PinPage));
        Routing.RegisterRoute("security", typeof(SecurityPage));
    }

    protected override void OnNavigating(ShellNavigatingEventArgs args)
    {
        KeyboardDismissal.Hide();
        base.OnNavigating(args);
    }

    protected override void OnNavigated(ShellNavigatedEventArgs args)
    {
        base.OnNavigated(args);
        Dispatcher.Dispatch(KeyboardDismissal.HideWhenInputIsNotFocused);
        if (args.Source is ShellNavigationSource.ShellSectionChanged or ShellNavigationSource.ShellItemChanged or ShellNavigationSource.ShellContentChanged
            && Navigation.NavigationStack.Count > 1)
            Dispatcher.Dispatch(async () => await Navigation.PopToRootAsync(false));
    }
}
