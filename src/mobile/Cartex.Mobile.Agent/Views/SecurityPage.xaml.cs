using Cartex.Mobile.Agent.ViewModels;

namespace Cartex.Mobile.Agent.Views;

public partial class SecurityPage : ContentPage
{
    private readonly SecurityViewModel _vm;

    public SecurityPage(SecurityViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.Appear();
    }
}
