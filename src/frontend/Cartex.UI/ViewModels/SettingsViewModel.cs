using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    [ObservableProperty] private string _apiUrl;

    private UsersViewModel? _usersVm;
    private CustomersViewModel? _customersVm;
    private RolesViewModel? _rolesVm;

    public UsersViewModel UsersVm => _usersVm ??= ServiceLocator.Resolve<UsersViewModel>();
    public CustomersViewModel CustomersVm => _customersVm ??= ServiceLocator.Resolve<CustomersViewModel>();
    public RolesViewModel RolesVm => _rolesVm ??= ServiceLocator.Resolve<RolesViewModel>();

    public SettingsViewModel()
    {
        _apiUrl = SettingsService.Instance.ApiBaseUrl;
    }

    [RelayCommand]
    private void SaveApiUrl() => SettingsService.Instance.ApiBaseUrl = ApiUrl;
}
