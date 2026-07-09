using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Cartex.UI.Views;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
        AddHandler(PointerWheelChangedEvent, OnWheel, RoutingStrategies.Tunnel);
    }

    private void OnWheel(object? sender, PointerWheelEventArgs e)
    {
        PageScroll.Offset = PageScroll.Offset.WithY(PageScroll.Offset.Y - e.Delta.Y * 120);
        e.Handled = true;
    }
}
