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

    private void OnEntryCompleted(object? sender, EventArgs e)
    {
        var next = sender switch
        {
            Entry entry when entry == ProductNameEntry => _vm.CanChoosePriceCurrency ? PriceWithCurrencyEntry : PriceEntry,
            Entry entry when entry == PriceWithCurrencyEntry || entry == PriceEntry => CodeEntry,
            Entry entry when entry == NewBarcodeEntry => NewBarcodePackQtyEntry,
            _ => null
        };

        if (next is not null)
            next.Focus();
        else if (sender is Entry entry)
            entry.Unfocus();
    }
}
