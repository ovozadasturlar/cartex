using Avalonia.Controls;
using Cartex.UI.ViewModels;

namespace Cartex.UI.Views;

public partial class SalesHistoryView : UserControl
{
    public SalesHistoryView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is SalesHistoryViewModel vm)
                await vm.LoadCommand.ExecuteAsync(null);
        };
    }
}
