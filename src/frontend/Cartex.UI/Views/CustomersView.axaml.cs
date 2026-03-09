using Avalonia.Controls;
using Cartex.UI.ViewModels;
using Cartex.Shared.Models.Customers;

namespace Cartex.UI.Views;

public partial class CustomersView : UserControl
{
    public CustomersView()
    {
        InitializeComponent();

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
}
