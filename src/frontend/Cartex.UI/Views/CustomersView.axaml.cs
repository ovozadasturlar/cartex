using Avalonia.Controls;
using Cartex.UI.ViewModels;

namespace Cartex.UI.Views;

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
