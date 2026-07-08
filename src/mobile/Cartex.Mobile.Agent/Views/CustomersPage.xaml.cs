using Cartex.Mobile.Agent.ViewModels;

namespace Cartex.Mobile.Agent.Views;

public partial class CustomersPage : ContentPage
{
    private readonly CustomersViewModel _vm;

    public CustomersPage(CustomersViewModel vm)
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
