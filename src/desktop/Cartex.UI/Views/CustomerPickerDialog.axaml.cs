using Avalonia.Controls;
using Cartex.UI.ViewModels;

namespace Cartex.UI.Views;

public partial class CustomerPickerDialog : UserControl
{
    public CustomerPickerDialog()
    {
        InitializeComponent();
        // Every call site would otherwise have to remember to seed the list before showing
        // the dialog, so the dialog loads it itself.
        Loaded += async (_, _) =>
        {
            if (DataContext is CustomerPickerViewModel vm) await vm.InitAsync();
        };
    }
}
