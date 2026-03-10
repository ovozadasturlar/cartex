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
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        LocalizationManager.Instance.PropertyChanged += OnLanguageChanged;
        UpdateHeaders();

        if (this.FindControl<DataGrid>("UsersGrid") is { } grid)
            grid.SelectionChanged += OnGridSelectionChanged;
    }

    private void OnUnloaded(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        LocalizationManager.Instance.PropertyChanged -= OnLanguageChanged;

        if (this.FindControl<DataGrid>("UsersGrid") is { } grid)
            grid.SelectionChanged -= OnGridSelectionChanged;
    }

    private void OnGridSelectionChanged(object? s, SelectionChangedEventArgs e)
    {
        if (s is DataGrid dg && DataContext is UsersViewModel vm)
            vm.SelectedUser = dg.SelectedItem as UserDto;
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
