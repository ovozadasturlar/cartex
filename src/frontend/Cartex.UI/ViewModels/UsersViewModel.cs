using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Users;

namespace Cartex.UI.ViewModels;

public partial class UsersViewModel : ViewModelBase
{
    private readonly IUsersApi _usersApi;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private UserDto? _selectedUser;

    public ObservableCollection<UserDto> Users { get; } = [];

    public UsersViewModel(IUsersApi usersApi)
    {
        _usersApi = usersApi;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var users = await _usersApi.GetAllAsync();
            Users.Clear();
            foreach (var u in users)
                Users.Add(u);
        }
        catch { }
        finally
        {
            IsLoading = false;
        }
    }
}
