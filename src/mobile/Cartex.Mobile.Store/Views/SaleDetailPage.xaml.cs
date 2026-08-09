using Cartex.Mobile.Store.ViewModels;

namespace Cartex.Mobile.Store.Views;

public partial class SaleDetailPage : ContentPage
{
    private readonly SaleDetailViewModel _viewModel;
    private bool _hasAppeared;

    public SaleDetailPage(SaleDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_hasAppeared)
            await _viewModel.ReloadAsync();
        else
        {
            _hasAppeared = true;
            await _viewModel.AppearAsync();
        }
    }
}
