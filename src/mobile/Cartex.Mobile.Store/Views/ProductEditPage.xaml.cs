using Cartex.Mobile.Store.ViewModels;

namespace Cartex.Mobile.Store.Views;

public partial class ProductEditPage : ContentPage
{
    private readonly ProductEditViewModel _vm;

    public ProductEditPage(ProductEditViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = _vm.AppearAsync();
    }
}
