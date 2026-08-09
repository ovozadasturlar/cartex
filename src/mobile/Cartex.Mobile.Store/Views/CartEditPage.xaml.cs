using Cartex.Mobile.Store.ViewModels;

namespace Cartex.Mobile.Store.Views;

public partial class CartEditPage : ContentPage
{
    private readonly CartEditViewModel _viewModel;

    public CartEditPage(CartEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.AppearAsync();
    }

    private void OnQuantityUnfocused(object? sender, FocusEventArgs e)
    {
        if (sender is Entry { BindingContext: CartEditLine line })
            _viewModel.CommitQuantityCommand.Execute(line);
    }
}
