using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Cartex.UI.ViewModels;

namespace Cartex.UI.Views;

public partial class SuppliesView : UserControl
{
    private SuppliesViewModel? _vm;

    public SuppliesView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Controls.FocusNavigator.Attach(this,
            LineBarcodeBox, ProductBox, LineQtyBox, LineEntryBox, LinePriceBox, LineSellingBox, LineExpiryBox, LineAddButton);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm is not null)
        {
            _vm.FocusProductRequested -= FocusProduct;
            _vm.FocusPrintQuantityRequested -= FocusPrintQuantity;
            _vm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _vm = DataContext as SuppliesViewModel;

        if (_vm is not null)
        {
            _vm.FocusProductRequested += FocusProduct;
            _vm.FocusPrintQuantityRequested += FocusPrintQuantity;
            _vm.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SuppliesViewModel.IsPrintOpen) || _vm?.IsPrintOpen != true) return;

        Dispatcher.UIThread.Post(() =>
        {
            if (_vm?.HasManyBarcodes == true) PrintChips.Focus();
            else FocusPrintQuantity();
        }, DispatcherPriority.Background);
    }

    private void FocusProduct() => Dispatcher.UIThread.Post(() => Select(ProductBox));

    private void FocusPrintQuantity() =>
        Dispatcher.UIThread.Post(() => Select(PrintQuantityBox), DispatcherPriority.Background);

    private static void Select(Control control)
    {
        control.Focus();
        if (control.GetVisualDescendants().OfType<TextBox>().FirstOrDefault() is { } textBox)
        {
            textBox.Focus();
            textBox.SelectAll();
        }
    }
}
