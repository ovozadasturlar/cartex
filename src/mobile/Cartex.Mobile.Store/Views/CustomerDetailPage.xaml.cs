using Cartex.Mobile.Store.ViewModels;

namespace Cartex.Mobile.Store.Views;

public partial class CustomerDetailPage : ContentPage
{
    private readonly CustomerDetailViewModel _viewModel;

    public CustomerDetailPage(CustomerDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.AppearAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        if (_viewModel.IsPaymentOpen)
        {
            _viewModel.ClosePaymentCommand.Execute(null);
            return true;
        }
        return base.OnBackButtonPressed();
    }
}
