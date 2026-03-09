using Avalonia.Controls;
using Cartex.UI.Services;
using Cartex.UI.ViewModels;
using Cartex.Shared.Models.Products;
using System.ComponentModel;

namespace Cartex.UI.Views;

public partial class ProductsView : UserControl
{
    public ProductsView()
    {
        InitializeComponent();
        LocalizationManager.Instance.PropertyChanged += OnLanguageChanged;

        var grid = this.FindControl<DataGrid>("ProductsGrid");
        if (grid is not null)
            grid.SelectionChanged += (s, _) =>
            {
                if (s is DataGrid dg && DataContext is ProductsViewModel vm)
                    vm.SelectedProduct = dg.SelectedItem as ProductDto;
            };

        Loaded += async (_, _) =>
        {
            UpdateHeaders();
            if (DataContext is ProductsViewModel vm)
                await vm.LoadCommand.ExecuteAsync(null);
        };
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "Item[]") UpdateHeaders();
    }

    private void UpdateHeaders()
    {
        if (ProductsGrid.Columns.Count < 6) return;
        var l = LocalizationManager.Instance;
        ProductsGrid.Columns[1].Header = l["name"];
        ProductsGrid.Columns[2].Header = l["category"];
        ProductsGrid.Columns[3].Header = l["unit"];
        ProductsGrid.Columns[4].Header = l["min_stock"];
        ProductsGrid.Columns[5].Header = l["barcode"];
    }
}
