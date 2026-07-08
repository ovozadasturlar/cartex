using System;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Cartex.UI.Services;
using Cartex.UI.ViewModels;

namespace Cartex.UI.Views;

public partial class DesktopShell : UserControl
{
    private TopLevel? _keyHost;

    public DesktopShell()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        var top = TopLevel.GetTopLevel(this);
        if (top is not null)
        {
            var manager = new WindowNotificationManager(top)
            {
                Position = NotificationPosition.TopRight,
                MaxItems = 4
            };
            ServiceLocator.Resolve<ToastService>().Attach(manager);
            ServiceLocator.Resolve<FilePickerService>().Attach(top);

            if (_keyHost is null)
            {
                _keyHost = top;
                _keyHost.AddHandler(KeyDownEvent, OnShellKeyDown, RoutingStrategies.Tunnel);
            }
        }
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        if (_keyHost is not null)
        {
            _keyHost.RemoveHandler(KeyDownEvent, OnShellKeyDown);
            _keyHost = null;
        }
        base.OnUnloaded(e);
    }

    private void OnShellKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        if (e.Key == Key.K && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            vm.OpenPaletteCommand.Execute(null);
            Dispatcher.UIThread.Post(() => this.FindControl<TextBox>("PaletteInput")?.Focus());
            e.Handled = true;
            return;
        }

        if (!vm.IsPaletteOpen) return;

        switch (e.Key)
        {
            case Key.Escape: vm.ClosePaletteCommand.Execute(null); e.Handled = true; break;
            case Key.Enter: vm.ExecutePaletteCommand.Execute(vm.SelectedPaletteItem); e.Handled = true; break;
            case Key.Down: MovePalette(vm, 1); e.Handled = true; break;
            case Key.Up: MovePalette(vm, -1); e.Handled = true; break;
        }
    }

    private void OnPaletteItemActivated(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.ExecutePaletteCommand.Execute(vm.SelectedPaletteItem);
    }

    private static void MovePalette(MainViewModel vm, int delta)
    {
        if (vm.PaletteResults.Count == 0) return;
        var idx = vm.SelectedPaletteItem is null ? -1 : vm.PaletteResults.IndexOf(vm.SelectedPaletteItem);
        idx = Math.Clamp(idx + delta, 0, vm.PaletteResults.Count - 1);
        vm.SelectedPaletteItem = vm.PaletteResults[idx];
    }
}
