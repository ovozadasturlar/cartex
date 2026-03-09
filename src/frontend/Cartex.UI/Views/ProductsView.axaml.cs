using Avalonia.Controls;
using Cartex.UI.ViewModels;
using Cartex.Shared.Models.Products;

namespace Cartex.UI.Views;

public partial class ProductsView : UserControl
{
    public ProductsView()
    {
        InitializeComponent();

        var grid = this.FindControl<DataGrid>("ProductsGrid");
        if (grid is not null)
            grid.SelectionChanged += (s, _) =>
            {
                if (s is DataGrid dg && DataContext is ProductsViewModel vm)
                    vm.SelectedProduct = dg.SelectedItem as ProductDto;
            };

        Loaded += async (_, _) =>
        {
            if (DataContext is ProductsViewModel vm)
                await vm.LoadCommand.ExecuteAsync(null);
        };
    }
}
