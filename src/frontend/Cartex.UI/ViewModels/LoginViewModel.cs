using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    private readonly AuthService _authService;
    private readonly NavigationService _navigationService;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isLoading;

    public LoginViewModel(AuthService authService, NavigationService navigationService)
    {
        _authService = authService;
        _navigationService = navigationService;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        ErrorMessage = null;
        IsLoading = true;

        try
        {
            await _authService.LoginAsync(Username, Password);
            var mainVm = ServiceLocator.Resolve<MainViewModel>();
            mainVm.Initialize();
            _navigationService.NavigateTo(mainVm);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message.Contains("401") || ex.Message.Contains("Unauthorized")
                ? L["login_error"]
                : ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
