using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Cartex.UI.ViewModels;

namespace Cartex.UI.Views;

public partial class ProductsView : UserControl
{
    public ProductsView()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (DataContext is ProductsViewModel vm)
        {
            vm.EditNameFocusRequested -= FocusEditName;
            vm.EditNameFocusRequested += FocusEditName;
            vm.FocusPrintQuantityRequested -= FocusPrintQuantity;
            vm.FocusPrintQuantityRequested += FocusPrintQuantity;
            vm.PropertyChanged -= OnViewModelPropertyChanged;
            vm.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        if (DataContext is ProductsViewModel vm)
        {
            vm.EditNameFocusRequested -= FocusEditName;
            vm.FocusPrintQuantityRequested -= FocusPrintQuantity;
            vm.PropertyChanged -= OnViewModelPropertyChanged;
        }
        base.OnUnloaded(e);
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ProductsViewModel.IsPrintOpen) || DataContext is not ProductsViewModel { IsPrintOpen: true } vm) return;

        Dispatcher.UIThread.Post(() =>
        {
            if (vm.HasManyBarcodes) PrintChips.Focus();
            else FocusPrintQuantity();
        }, DispatcherPriority.Background);
    }

    private void FocusEditName() => this.FindControl<TextBox>("EditNameBox")?.Focus();

    private void FocusPrintQuantity() =>
        Dispatcher.UIThread.Post(() =>
        {
            PrintQuantityBox.Focus();
            if (PrintQuantityBox.GetVisualDescendants().OfType<TextBox>().FirstOrDefault() is { } textBox)
            {
                textBox.Focus();
                textBox.SelectAll();
            }
        }, DispatcherPriority.Background);
}
