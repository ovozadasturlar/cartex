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
        }

        _vm = DataContext as SuppliesViewModel;

        if (_vm is not null)
        {
            _vm.FocusProductRequested += FocusProduct;
        }
    }

    private void FocusProduct() => Dispatcher.UIThread.Post(() => Select(ProductBox));

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
