using Cartex.Mobile.Agent.ViewModels;

namespace Cartex.Mobile.Agent.Views;

public partial class CustomerCreatePage : ContentPage
{
    public CustomerCreatePage(CustomerCreateViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
