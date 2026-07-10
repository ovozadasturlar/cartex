using Cartex.Mobile.Agent.ViewModels;

namespace Cartex.Mobile.Agent.Views;

public partial class PinPage : ContentPage
{
    private readonly PinViewModel _vm;

    public PinPage(PinViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.AppearAsync();
    }

    protected override bool OnBackButtonPressed() => true;
}
