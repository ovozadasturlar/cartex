using Cartex.Mobile.Store.ViewModels;

namespace Cartex.Mobile.Store.Views;

public partial class ReceiveCartPage : ContentPage
{
    private readonly ReceiveCartViewModel _vm;

    public ReceiveCartPage(ReceiveCartViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.Appear();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.Disappear();
    }
}
