using Cartex.Mobile.Agent.ViewModels;

namespace Cartex.Mobile.Agent.Views;

public partial class OutboxPage : ContentPage
{
    private readonly OutboxViewModel _vm;

    public OutboxPage(OutboxViewModel vm)
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
