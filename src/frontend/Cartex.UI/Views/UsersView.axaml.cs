using Avalonia.Controls;
using Cartex.UI.ViewModels;
using Cartex.Shared.Models.Users;

namespace Cartex.UI.Views;

public partial class UsersView : UserControl
{
    public UsersView()
    {
        InitializeComponent();

        var grid = this.FindControl<DataGrid>("UsersGrid");
        if (grid is not null)
            grid.SelectionChanged += (s, _) =>
            {
                if (s is DataGrid dg && DataContext is UsersViewModel vm)
                    vm.SelectedUser = dg.SelectedItem as UserDto;
            };

        Loaded += async (_, _) =>
        {
            if (DataContext is UsersViewModel vm)
                await vm.LoadCommand.ExecuteAsync(null);
        };
    }
}
