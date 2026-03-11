using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Cartex.UI.ViewModels;

namespace Cartex.UI.Views;

public partial class SalesView : UserControl
{
    public SalesView()
    {
        InitializeComponent();
        WireScrollButtons();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is SalesViewModel vm)
        {
            vm.PropertyChanged += OnVmPropertyChanged;
            UpdatePaymentRowHeight(vm.IsNumpadVisible);
        }
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SalesViewModel.IsNumpadVisible) && sender is SalesViewModel vm)
            UpdatePaymentRowHeight(vm.IsNumpadVisible);
    }

    private void UpdatePaymentRowHeight(bool numpadVisible)
    {
        if (DesktopRightGrid is null) return;
        var row = DesktopRightGrid.RowDefinitions[2];
        row.Height = numpadVisible ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
        row.MinHeight = numpadVisible ? 450 : 0;
        DesktopRightGrid.InvalidateMeasure();
        DesktopRightGrid.InvalidateArrange();
    }

    private void WireScrollButtons()
    {
        WirePair(CatScrollLeft, CatScrollRight, CatScroll);
        WirePair(TouchCatLeft, TouchCatRight, TouchCatScroll);
    }

    private static void WirePair(Button? left, Button? right, ScrollViewer? sv)
    {
        if (sv is null) return;
        UpdateArrowStates(left, right, sv);
        sv.ScrollChanged += (_, _) => UpdateArrowStates(left, right, sv);
        left?.AddHandler(Button.ClickEvent, (_, _) =>
        {
            sv.Offset = sv.Offset.WithX(Math.Max(0, sv.Offset.X - 150));
            UpdateArrowStates(left, right, sv);
        });
        right?.AddHandler(Button.ClickEvent, (_, _) =>
        {
            sv.Offset = sv.Offset.WithX(sv.Offset.X + 150);
            UpdateArrowStates(left, right, sv);
        });
    }

    private static void UpdateArrowStates(Button? left, Button? right, ScrollViewer sv)
    {
        if (left is not null) left.IsEnabled = sv.Offset.X > 0;
        if (right is not null) right.IsEnabled = sv.Offset.X + sv.Viewport.Width < sv.Extent.Width - 1;
    }
}
