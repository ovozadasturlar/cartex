using Avalonia.Controls;
using Cartex.UI.Services;
using Cartex.UI.ViewModels;
using System.ComponentModel;

namespace Cartex.UI.Views;

public partial class SalesView : UserControl
{
    public SalesView()
    {
        InitializeComponent();
        UpdateHeaders();
        LocalizationManager.Instance.PropertyChanged += OnLanguageChanged;

        Loaded += async (_, _) =>
        {
            if (DataContext is SalesViewModel vm)
            {
                await vm.LoadStocksCommand.ExecuteAsync(null);
                await vm.LoadCustomersCommand.ExecuteAsync(null);
            }
        };
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "Item[]") UpdateHeaders();
    }

    private void UpdateHeaders()
    {
        var l = LocalizationManager.Instance;
        StocksGrid.Columns[0].Header = l["name"];
        StocksGrid.Columns[1].Header = l["quantity"];
        StocksGrid.Columns[2].Header = l["selling_price"];
    }
}
