using System.ComponentModel;
using System.Threading;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.Threading;
using Cartex.UI.ViewModels;

namespace Cartex.UI.Views;

public partial class DesktopShell : UserControl
{
    private double _sidebarWidth = 240;
    private MainViewModel? _vm;
    private CancellationTokenSource? _animCts;

    public DesktopShell()
    {
        InitializeComponent();
        SidebarBorder.Width = 240;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_vm != null) _vm.PropertyChanged -= OnVmPropertyChanged;
        _vm = DataContext as MainViewModel;
        if (_vm == null) return;
        _vm.PropertyChanged += OnVmPropertyChanged;
        _sidebarWidth = _vm.IsSidebarCollapsed ? 60 : 240;
        SidebarBorder.Width = _sidebarWidth;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsSidebarCollapsed))
            Dispatcher.UIThread.Post(() => AnimateSidebar(_vm?.IsSidebarCollapsed ?? false));
    }

    private async void AnimateSidebar(bool collapsed)
    {
        _animCts?.Cancel();
        _animCts = new CancellationTokenSource();

        var target = collapsed ? 60.0 : 240.0;
        var from = _sidebarWidth;
        _sidebarWidth = target;

        var anim = new Avalonia.Animation.Animation
        {
            Duration = TimeSpan.FromMilliseconds(250),
            Easing = new CubicEaseInOut(),
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame { Cue = new Cue(0d), Setters = { new Setter(Border.WidthProperty, from) } },
                new KeyFrame { Cue = new Cue(1d), Setters = { new Setter(Border.WidthProperty, target) } }
            }
        };

        try { await anim.RunAsync(SidebarBorder, _animCts.Token); }
        catch (OperationCanceledException) { }
    }
}
