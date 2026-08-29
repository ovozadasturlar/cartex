using Cartex.Mobile.Store.ViewModels;

namespace Cartex.Mobile.Store.Views;

public partial class OfflineSettingsPage : ContentPage
{
    private readonly OfflineSettingsViewModel _vm;

    public OfflineSettingsPage(OfflineSettingsViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.AppearAsync();
    }

    protected override void OnDisappearing()
    {
        _vm.Disappear();
        base.OnDisappearing();
    }
}
