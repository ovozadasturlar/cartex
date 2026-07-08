using Cartex.Mobile.Agent.ViewModels;

namespace Cartex.Mobile.Agent.Views;

public partial class CustomerPage : ContentPage
{
    private readonly CustomerViewModel _vm;

    public CustomerPage(CustomerViewModel vm)
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
