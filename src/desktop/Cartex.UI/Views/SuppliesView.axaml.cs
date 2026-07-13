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
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm is not null) _vm.FocusProductRequested -= FocusProduct;
        _vm = DataContext as SuppliesViewModel;
        if (_vm is not null) _vm.FocusProductRequested += FocusProduct;
    }

    // Yangi mahsulot yaratish rad etilsa, fokus nom maydoniga qaytadi va matn tanlanadi.
    private void FocusProduct() => Dispatcher.UIThread.Post(() =>
    {
        ProductBox.Focus();
        if (ProductBox.GetVisualDescendants().OfType<TextBox>().FirstOrDefault() is { } textBox)
            textBox.SelectAll();
    });
}
