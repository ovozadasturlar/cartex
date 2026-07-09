using Avalonia.Controls;
using Avalonia.Interactivity;
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
        }
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        if (DataContext is ProductsViewModel vm)
            vm.EditNameFocusRequested -= FocusEditName;
        base.OnUnloaded(e);
    }

    private void FocusEditName() => this.FindControl<TextBox>("EditNameBox")?.Focus();
}
