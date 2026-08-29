using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Cartex.UI.ViewModels;

namespace Cartex.UI.Views;

public partial class BarcodeLabelDialog : UserControl
{
    private BarcodeLabelSession? _session;

    public BarcodeLabelDialog()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_session is not null)
        {
            _session.FocusBarcodesRequested -= FocusBarcodes;
            _session.FocusQuantityRequested -= FocusQuantity;
        }

        _session = DataContext as BarcodeLabelSession;

        if (_session is not null)
        {
            _session.FocusBarcodesRequested += FocusBarcodes;
            _session.FocusQuantityRequested += FocusQuantity;
        }
    }

    private void FocusBarcodes() => Dispatcher.UIThread.Post(() => BarcodeChoices.Focus(), DispatcherPriority.Background);

    private void FocusQuantity() => Dispatcher.UIThread.Post(() =>
    {
        QuantityBox.Focus();
        if (QuantityBox.GetVisualDescendants().OfType<TextBox>().FirstOrDefault() is { } textBox)
        {
            textBox.Focus();
            textBox.SelectAll();
        }
    }, DispatcherPriority.Background);
}
