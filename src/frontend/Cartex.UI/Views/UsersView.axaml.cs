using Avalonia.Controls;
using Cartex.UI.Services;
using Cartex.UI.ViewModels;
using Cartex.Shared.Models.Users;
using System.ComponentModel;

namespace Cartex.UI.Views;

public partial class UsersView : UserControl
{
    public UsersView()
    {
        InitializeComponent();
        LocalizationManager.Instance.PropertyChanged += OnLanguageChanged;

        var grid = this.FindControl<DataGrid>("UsersGrid");
        if (grid is not null)
            grid.SelectionChanged += (s, _) =>
            {
                if (s is DataGrid dg && DataContext is UsersViewModel vm)
                    vm.SelectedUser = dg.SelectedItem as UserDto;
            };

        Loaded += async (_, _) =>
        {
            UpdateHeaders();
            if (DataContext is UsersViewModel vm)
                await vm.LoadCommand.ExecuteAsync(null);
        };
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "Item[]") UpdateHeaders();
    }

    private void UpdateHeaders()
    {
        if (UsersGrid.Columns.Count < 6) return;
        var l = LocalizationManager.Instance;
        UsersGrid.Columns[1].Header = l["full_name"];
        UsersGrid.Columns[2].Header = l["username"];
        UsersGrid.Columns[3].Header = l["role"];
        UsersGrid.Columns[4].Header = l["shop"];
        UsersGrid.Columns[5].Header = l["active"];
    }
}
