using Avalonia.Controls;
using Cartex.Desktop.ViewModels;

namespace Cartex.Desktop.Views;

public partial class CustomersView : UserControl
{
    public CustomersView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is CustomersViewModel vm)
                await vm.LoadCommand.ExecuteAsync(null);
        };
    }
}
