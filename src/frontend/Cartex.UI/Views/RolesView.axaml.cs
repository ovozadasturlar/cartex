using Avalonia.Controls;
using Cartex.UI.Services;
using Cartex.UI.ViewModels;
using Cartex.Shared.Models.Roles;
using System.ComponentModel;

namespace Cartex.UI.Views;

public partial class RolesView : UserControl
{
    public RolesView()
    {
        InitializeComponent();
        UpdateHeaders();
        LocalizationManager.Instance.PropertyChanged += OnLanguageChanged;

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

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "Item[]") UpdateHeaders();
    }

    private void UpdateHeaders()
    {
        var l = LocalizationManager.Instance;
        RolesGrid.Columns[1].Header = l["name"];
        RolesGrid.Columns[2].Header = l["description"];
    }
}
