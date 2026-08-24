using Cartex.Mobile.Store.ViewModels;

namespace Cartex.Mobile.Store.Views;

public partial class SmsGatewayPage : ContentPage
{
    private readonly SmsGatewayViewModel _viewModel;

    public SmsGatewayPage(SmsGatewayViewModel viewModel)
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
