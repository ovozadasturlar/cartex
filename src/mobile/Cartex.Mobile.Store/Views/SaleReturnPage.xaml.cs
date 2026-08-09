using Cartex.Mobile.Store.ViewModels;

namespace Cartex.Mobile.Store.Views;

public partial class SaleReturnPage : ContentPage
{
    private readonly SaleReturnViewModel _viewModel;

    public SaleReturnPage(SaleReturnViewModel viewModel)
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
        if (sender is Entry { BindingContext: SaleReturnLine line })
            _viewModel.CommitQuantityCommand.Execute(line);
    }
}
