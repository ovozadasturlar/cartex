using Avalonia.Controls;
using Cartex.UI.Services;
using Cartex.UI.ViewModels;
using System.ComponentModel;

namespace Cartex.UI.Views;

public partial class WarehouseView : UserControl
{
    public WarehouseView()
    {
        InitializeComponent();
        UpdateHeaders();
        LocalizationManager.Instance.PropertyChanged += OnLanguageChanged;

        Loaded += async (_, _) =>
        {
            if (DataContext is WarehouseViewModel vm)
                await vm.LoadWarehousesCommand.ExecuteAsync(null);
        };
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "Item[]") UpdateHeaders();
    }

    private void UpdateHeaders()
    {
        var l = LocalizationManager.Instance;
        StocksGrid.Columns[1].Header = l["name"];
        StocksGrid.Columns[2].Header = l["unit"];
        StocksGrid.Columns[3].Header = l["quantity"];
        StocksGrid.Columns[4].Header = l["purchase_price"];
        StocksGrid.Columns[5].Header = l["selling_price"];
        StocksGrid.Columns[6].Header = l["expired_at"];
    }
}
