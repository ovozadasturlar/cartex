using Cartex.Mobile.Store.ViewModels;

namespace Cartex.Mobile.Store.Views;

public partial class CustomerStatementPage : ContentPage
{
    private readonly CustomerStatementViewModel _viewModel;
    public CustomerStatementPage(CustomerStatementViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.AppearAsync();
    }
}
