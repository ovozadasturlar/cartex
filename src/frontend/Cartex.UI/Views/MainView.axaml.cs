using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Cartex.UI.ViewModels;

namespace Cartex.UI.Views;

public partial class MainView : UserControl
{
    private readonly DesktopShell _desktopShell = new();
    private readonly TouchShell _touchShell = new();

    public MainView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.PropertyChanged += OnViewModelPropertyChanged;
            UpdateShell(vm);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsTouchMode) && sender is MainViewModel vm)
            UpdateShell(vm);
    }

    private void UpdateShell(MainViewModel vm)
    {
        var shell = vm.IsTouchMode ? (UserControl)_touchShell : _desktopShell;
        shell.DataContext = vm;
        ShellHost.Content = shell;
    }
}
