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

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        FocusScan();
    }

    private void FocusScan() => this.FindControl<TextBox>("ScanBox")?.Focus();

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (DataContext is SalesViewModel vm)
        {
            switch (e.Key)
            {
                case Key.F2: FocusScan(); e.Handled = true; break;
                case Key.F4: vm.PayExactCommand.Execute(null); e.Handled = true; break;
                case Key.F6: vm.HoldSaleCommand.Execute(null); e.Handled = true; break;
                case Key.F9: vm.CompleteSaleCommand.Execute(null); e.Handled = true; break;
                case Key.Escape: vm.ClearCartCommand.Execute(null); e.Handled = true; break;
            }
        }
        base.OnKeyDown(e);
    }
}
