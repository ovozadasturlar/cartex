using Avalonia.Controls;
using Cartex.UI.ViewModels;
using Cartex.Shared.Models.Roles;

namespace Cartex.UI.Views;

public partial class RolesView : UserControl
{
    public RolesView()
    {
        InitializeComponent();

        var grid = this.FindControl<DataGrid>("RolesGrid");
        if (grid is not null)
            grid.SelectionChanged += (s, _) =>
            {
                if (s is DataGrid dg && DataContext is RolesViewModel vm)
                    vm.SelectedRole = dg.SelectedItem as RoleDto;
            };

        Loaded += async (_, _) =>
        {
            if (DataContext is RolesViewModel vm)
                await vm.LoadCommand.ExecuteAsync(null);
        };
    }
}
