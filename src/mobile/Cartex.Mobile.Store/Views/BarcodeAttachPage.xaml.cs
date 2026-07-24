using Cartex.Mobile.Store.ViewModels;

namespace Cartex.Mobile.Store.Views;

public partial class BarcodeAttachPage : ContentPage
{
    private readonly BarcodeAttachViewModel _vm;

    public BarcodeAttachPage(BarcodeAttachViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        SearchBox.Focus();
    }
}
