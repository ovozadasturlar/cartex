using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Cartex.UI.ViewModels;

namespace Cartex.UI.Views;

public partial class SalesView : UserControl
{
    public SalesView()
    {
        InitializeComponent();
    }

    private void OnProductsScroll(object? sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer sv || DataContext is not SalesViewModel vm) return;
        if (!vm.HasMoreProducts || vm.LoadMoreProductsCommand.IsRunning) return;
        if (sv.Offset.Y + sv.Viewport.Height >= sv.Extent.Height - 400)
            vm.LoadMoreProductsCommand.Execute(null);
    }

    private void OnChipsWheel(object? sender, PointerWheelEventArgs e)
    {
        if (sender is not ScrollViewer sv) return;
        sv.Offset = sv.Offset.WithX(sv.Offset.X - e.Delta.Y * 90);
        e.Handled = true;
    }

    private void OnChipsScroll(object? sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer sv) return;
        var (left, right) = ArrowsFor(sv.Name);
        if (left is null || right is null) return;
        var overflow = sv.Extent.Width > sv.Viewport.Width + 1;
        left.IsVisible = overflow;
        right.IsVisible = overflow;
        left.IsEnabled = sv.Offset.X > 1;
        right.IsEnabled = sv.Offset.X + sv.Viewport.Width < sv.Extent.Width - 1;
    }

    private (Button?, Button?) ArrowsFor(string? scrollerName) => scrollerName switch
    {
        "CatScroll" => (this.FindControl<Button>("CatLeft"), this.FindControl<Button>("CatRight")),
        "SubCatScroll" => (this.FindControl<Button>("SubLeft"), this.FindControl<Button>("SubRight")),
        _ => (null, null)
    };

    private void OnChipLeft(object? sender, RoutedEventArgs e) => NudgeChips(sender, -260);

    private void OnChipRight(object? sender, RoutedEventArgs e) => NudgeChips(sender, 260);

    private void NudgeChips(object? sender, double delta)
    {
        var name = (sender as Control)?.Name is "SubLeft" or "SubRight" ? "SubCatScroll" : "CatScroll";
        var sv = this.FindControl<ScrollViewer>(name);
        if (sv is null) return;
        var max = Math.Max(0, sv.Extent.Width - sv.Viewport.Width);
        sv.Offset = sv.Offset.WithX(Math.Clamp(sv.Offset.X + delta, 0, max));
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (DataContext is SalesViewModel vm)
        {
            vm.ScanFocusRequested -= FocusScan;
            vm.ScanFocusRequested += FocusScan;
        }
        FocusScan();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        if (DataContext is SalesViewModel vm)
            vm.ScanFocusRequested -= FocusScan;
        base.OnUnloaded(e);
    }

    private void FocusScan() => this.FindControl<TextBox>("ScanBox")?.Focus();
}
