using Avalonia.Controls;
using Cartex.UI.Services;
using Cartex.UI.ViewModels;
using Cartex.Shared.Models.Customers;
using System.ComponentModel;

namespace Cartex.UI.Views;

public partial class CustomersView : UserControl
{
    public CustomersView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        LocalizationManager.Instance.PropertyChanged += OnLanguageChanged;
        UpdateHeaders();

        if (this.FindControl<DataGrid>("CustomersGrid") is { } grid)
            grid.SelectionChanged += OnGridSelectionChanged;
    }

    private void OnUnloaded(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        LocalizationManager.Instance.PropertyChanged -= OnLanguageChanged;

        if (this.FindControl<DataGrid>("CustomersGrid") is { } grid)
            grid.SelectionChanged -= OnGridSelectionChanged;
    }

    private void OnGridSelectionChanged(object? s, SelectionChangedEventArgs e)
    {
        if (s is DataGrid dg && DataContext is CustomersViewModel vm)
            vm.SelectedCustomer = dg.SelectedItem as CustomerDto;
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "Item[]") UpdateHeaders();
    }

    private void UpdateHeaders()
    {
        if (CustomersGrid.Columns.Count < 6) return;
        var l = LocalizationManager.Instance;
        CustomersGrid.Columns[1].Header = l["full_name"];
        CustomersGrid.Columns[2].Header = l["phone"];
        CustomersGrid.Columns[3].Header = l["card_barcode"];
        CustomersGrid.Columns[4].Header = l["discount_pct"];
        CustomersGrid.Columns[5].Header = l["cashback_balance"];
    }
}
