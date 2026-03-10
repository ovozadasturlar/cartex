using Avalonia.Controls;
using Cartex.UI.Services;
using System.ComponentModel;

namespace Cartex.UI.Views;

public partial class SalesView : UserControl
{
    public SalesView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        LocalizationManager.Instance.PropertyChanged += OnLanguageChanged;
        UpdateHeaders();
    }

    private void OnUnloaded(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        LocalizationManager.Instance.PropertyChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "Item[]") UpdateHeaders();
    }

    private void UpdateHeaders()
    {
        if (StocksGrid.Columns.Count < 3) return;
        var l = LocalizationManager.Instance;
        StocksGrid.Columns[0].Header = l["name"];
        StocksGrid.Columns[1].Header = l["quantity"];
        StocksGrid.Columns[2].Header = l["selling_price"];
    }
}
