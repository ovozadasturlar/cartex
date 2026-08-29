using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
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
                _keyHost.AddHandler(KeyUpEvent, OnShellKeyUp, RoutingStrategies.Tunnel);
                _keyHost.AddHandler(KeyDownEvent, OnPageShortcutKeyDown, RoutingStrategies.Bubble);
            }
        }
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        if (_keyHost is not null)
        {
            _keyHost.RemoveHandler(KeyDownEvent, OnShellKeyDown);
            _keyHost.RemoveHandler(KeyUpEvent, OnShellKeyUp);
            _keyHost.RemoveHandler(KeyDownEvent, OnPageShortcutKeyDown);
            _keyHost = null;
        }
        base.OnUnloaded(e);
    }

    private void OnShellKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        if (e.Key == Key.Tab && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            var direction = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? -1 : 1;
            if (vm.IsPageSwitcherOpen)
                vm.CyclePageSwitch(direction);
            else
                vm.BeginPageSwitch(direction);
            e.Handled = true;
            return;
        }

        if (vm.IsPageSwitcherOpen)
        {
            if (e.Key == Key.Escape)
            {
                vm.CancelPageSwitch();
                e.Handled = true;
            }
            return;
        }

        if (vm.IsShortcutHelpOpen)
        {
            if (e.Key is Key.Escape or Key.F1)
            {
                vm.ToggleShortcutHelpCommand.Execute(null);
                e.Handled = true;
            }
            return;
        }

        if (vm.IsOnboardingOpen) return;

        if (e.Key == Key.K && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            vm.OpenPaletteCommand.Execute(null);
            Dispatcher.UIThread.Post(() => this.FindControl<TextBox>("PaletteInput")?.Focus());
            e.Handled = true;
            return;
        }

        if (vm.IsPaletteOpen)
        {
            switch (e.Key)
            {
                case Key.Escape: vm.ClosePaletteCommand.Execute(null); e.Handled = true; break;
                case Key.Enter: vm.ExecutePaletteCommand.Execute(vm.SelectedPaletteItem); e.Handled = true; break;
                case Key.Down: MovePalette(vm, 1); e.Handled = true; break;
                case Key.Up: MovePalette(vm, -1); e.Handled = true; break;
            }
            return;
        }

        if (e.Key == Key.F1)
        {
            vm.ToggleShortcutHelpCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnShellKeyUp(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm || !vm.IsPageSwitcherOpen)
            return;
        if (e.Key is Key.LeftCtrl or Key.RightCtrl || !e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            vm.CommitPageSwitch();
            e.Handled = true;
        }
    }

    private void OnPageShortcutKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        if (vm.IsShortcutHelpOpen || vm.IsPaletteOpen || vm.IsOnboardingOpen) return;
        if (e.Source is Avalonia.Visual source && source.FindAncestorOfType<Ursa.Controls.OverlayDialogHost>() is not null) return;

        var inText = _keyHost?.FocusManager?.GetFocusedElement() is TextBox;
        if (ServiceLocator.Resolve<ShortcutService>().TryHandle(e, inText))
            e.Handled = true;
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
