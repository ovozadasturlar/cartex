using Cartex.Mobile.Agent.ViewModels;

namespace Cartex.Mobile.Agent.Views;

public partial class TransfersPage : ContentPage
{
    private readonly TransfersViewModel _vm;

    public TransfersPage(TransfersViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.AppearAsync();
    }
}
