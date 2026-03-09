using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly NavigationService _navigationService;

    [ObservableProperty]
    private string _welcomeMessage = string.Empty;

    public DashboardViewModel(NavigationService navigationService)
    {
        _navigationService = navigationService;
        WelcomeMessage = $"{L["welcome"]}, {ServiceLocator.Resolve<AuthService>().UserInfo?.FullName ?? ""}!";
    }

    [RelayCommand]
    private void GoToSales() => _navigationService.NavigateTo<SalesViewModel>();

    [RelayCommand]
    private void GoToProducts() => _navigationService.NavigateTo<ProductsViewModel>();

    [RelayCommand]
    private void GoToCustomers() => _navigationService.NavigateTo<CustomersViewModel>();
}
