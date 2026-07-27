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

    private void OnRowTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not BindableObject { BindingContext: AttachRow row })
            return;

        _vm.OpenDetailsCommand.Execute(row);
    }

    protected override bool OnBackButtonPressed()
    {
        if (_vm.IsDetailsOpen)
        {
            _vm.IsDetailsOpen = false;
            return true;
        }

        return base.OnBackButtonPressed();
    }
}
