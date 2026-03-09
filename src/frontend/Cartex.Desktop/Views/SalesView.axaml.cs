using Avalonia.Controls;
using Cartex.Desktop.ViewModels;

namespace Cartex.Desktop.Views;

public partial class SalesView : UserControl
{
    public SalesView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is SalesViewModel vm)
            {
                await vm.LoadStocksCommand.ExecuteAsync(null);
                await vm.LoadCustomersCommand.ExecuteAsync(null);
            }
        };
    }
}
