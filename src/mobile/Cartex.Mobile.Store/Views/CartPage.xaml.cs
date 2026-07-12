using Cartex.Mobile.Store.ViewModels;

namespace Cartex.Mobile.Store.Views;

public partial class CartPage : ContentPage
{
    private readonly CartViewModel _vm;

    public CartPage(CartViewModel vm)
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
        _vm.Disappear();
        base.OnDisappearing();
    }
}
