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
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        LocalizationManager.Instance.PropertyChanged += OnLanguageChanged;
        UpdateHeaders();

        if (this.FindControl<DataGrid>("RolesGrid") is { } grid)
            grid.SelectionChanged += OnGridSelectionChanged;
    }

    private void OnUnloaded(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        LocalizationManager.Instance.PropertyChanged -= OnLanguageChanged;

        if (this.FindControl<DataGrid>("RolesGrid") is { } grid)
            grid.SelectionChanged -= OnGridSelectionChanged;
    }

    private void OnGridSelectionChanged(object? s, SelectionChangedEventArgs e)
    {
        if (s is DataGrid dg && DataContext is RolesViewModel vm)
            vm.SelectedRole = dg.SelectedItem as RoleDto;
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "Item[]") UpdateHeaders();
    }

    private void UpdateHeaders()
    {
        if (RolesGrid.Columns.Count < 3) return;
        var l = LocalizationManager.Instance;
        RolesGrid.Columns[1].Header = l["name"];
        RolesGrid.Columns[2].Header = l["description"];
    }
}
