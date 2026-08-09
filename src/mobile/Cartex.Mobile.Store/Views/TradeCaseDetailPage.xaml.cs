using Cartex.Mobile.Store.ViewModels;

namespace Cartex.Mobile.Store.Views;

public partial class TradeCaseDetailPage : ContentPage
{
    private readonly TradeCaseDetailViewModel _viewModel;

    public TradeCaseDetailPage(TradeCaseDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.AppearAsync();
        Title = _viewModel.PageTitle;
    }
}
