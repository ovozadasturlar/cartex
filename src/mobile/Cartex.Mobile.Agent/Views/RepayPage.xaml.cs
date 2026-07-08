using Cartex.Mobile.Agent.ViewModels;

namespace Cartex.Mobile.Agent.Views;

public partial class RepayPage : ContentPage
{
    private readonly RepayViewModel _vm;

    public RepayPage(RepayViewModel vm)
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
