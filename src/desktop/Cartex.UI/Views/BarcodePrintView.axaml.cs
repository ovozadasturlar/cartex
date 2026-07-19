using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Cartex.UI.ViewModels;

namespace Cartex.UI.Views;

public partial class BarcodePrintView : UserControl
{
    private BarcodePrintViewModel? _vm;

    public BarcodePrintView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Controls.FocusNavigator.Attach(this, SearchBox, ProductsList, ChipsList, QuantityBox, PrintButton);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm is not null)
        {
            _vm.FocusChipsRequested -= FocusChips;
            _vm.FocusQuantityRequested -= FocusQuantity;
        }

        _vm = DataContext as BarcodePrintViewModel;

        if (_vm is not null)
        {
            _vm.FocusChipsRequested += FocusChips;
            _vm.FocusQuantityRequested += FocusQuantity;
        }
    }

    private void FocusChips() =>
        Dispatcher.UIThread.Post(() => ChipsList.Focus(), DispatcherPriority.Background);

    private void FocusQuantity() =>
        Dispatcher.UIThread.Post(() => Select(QuantityBox), DispatcherPriority.Background);

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
