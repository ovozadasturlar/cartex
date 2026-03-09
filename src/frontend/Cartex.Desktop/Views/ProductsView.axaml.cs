using Avalonia.Controls;
using Cartex.Desktop.ViewModels;

namespace Cartex.Desktop.Views;

public partial class ProductsView : UserControl
{
    public ProductsView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is ProductsViewModel vm)
                await vm.LoadCommand.ExecuteAsync(null);
        };
    }
}
