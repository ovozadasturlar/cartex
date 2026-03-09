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
        UpdateHeaders();
        LocalizationManager.Instance.PropertyChanged += OnLanguageChanged;

        var grid = this.FindControl<DataGrid>("CustomersGrid");
        if (grid is not null)
            grid.SelectionChanged += (s, _) =>
            {
                if (s is DataGrid dg && DataContext is CustomersViewModel vm)
                    vm.SelectedCustomer = dg.SelectedItem as CustomerDto;
            };

        Loaded += async (_, _) =>
        {
            if (DataContext is CustomersViewModel vm)
                await vm.LoadCommand.ExecuteAsync(null);
        };
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "Item[]") UpdateHeaders();
    }

    private void UpdateHeaders()
    {
        var l = LocalizationManager.Instance;
        CustomersGrid.Columns[1].Header = l["full_name"];
        CustomersGrid.Columns[2].Header = l["phone"];
        CustomersGrid.Columns[3].Header = l["card_barcode"];
        CustomersGrid.Columns[4].Header = l["discount_pct"];
        CustomersGrid.Columns[5].Header = l["cashback_balance"];
    }
}
