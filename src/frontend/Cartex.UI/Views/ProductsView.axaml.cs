using Avalonia.Controls;
using Cartex.UI.ViewModels;

namespace Cartex.UI.Views;

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
