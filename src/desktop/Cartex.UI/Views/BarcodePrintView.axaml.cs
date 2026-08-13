using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Cartex.UI.ViewModels;

namespace Cartex.UI.Views;

public partial class BarcodePrintView : UserControl
{
    private BarcodePrintViewModel? _vm;

    /// A copy count never reaches five digits, so anything longer landing in the box is a
    /// second scan - it goes to the search field instead of printing that many labels.
    private const int ScannedCodeLength = 5;
    private bool _redirectingScan;

    public BarcodePrintView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        QuantityBox.PropertyChanged += OnQuantityBoxPropertyChanged;
        Controls.FocusNavigator.Attach(this, SearchBox, ProductsList, ChipsList, QuantityBox, PrintButton);
    }

    private void OnQuantityBoxPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != NumericUpDown.TextProperty || _redirectingScan || _vm is null) return;

        var typed = e.GetNewValue<string?>()?.Trim();
        if (typed is not { Length: >= ScannedCodeLength } || !typed.All(char.IsDigit)) return;

        _redirectingScan = true;
        _vm.Quantity = 1;
        _vm.Search = typed;
        Dispatcher.UIThread.Post(() =>
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            _redirectingScan = false;
        }, DispatcherPriority.Background);
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
