using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Cartex.Shared.Models.Stocks;
using Cartex.UI.ViewModels;

namespace Cartex.UI.Views;

public partial class SalesView : UserControl
{
    public SalesView()
    {
        InitializeComponent();
    }

    private void OnTileHolding(object? sender, HoldingRoutedEventArgs e)
    {
        if (sender is Control { DataContext: StockOnHandDto product } && DataContext is SalesViewModel vm)
        {
            vm.ShowProductDetail(product);
            e.Handled = true;
        }
    }

    private void OnProductsScroll(object? sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer sv || DataContext is not SalesViewModel vm) return;
        if (!vm.HasMoreProducts || vm.LoadMoreProductsCommand.IsRunning) return;
        if (sv.Offset.Y + sv.Viewport.Height >= sv.Extent.Height - 400)
            vm.LoadMoreProductsCommand.Execute(null);
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
