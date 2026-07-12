using Cartex.Mobile.Agent.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.Mobile.Core;

namespace Cartex.Mobile.Agent.ViewModels;

public partial class ChangePasswordViewModel(MobileAuthService auth) : ObservableObject
{
    [ObservableProperty] private string _current = "";
    [ObservableProperty] private string _next = "";
    [ObservableProperty] private string _repeat = "";
    [ObservableProperty] private string? _error;
    [ObservableProperty] private bool _isBusy;

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;
        Error = null;
        if (string.IsNullOrEmpty(Current) || string.IsNullOrEmpty(Next)) { Error = Loc.Instance["err_fill_all"]; return; }
        if (Next.Length < 6) { Error = Loc.Instance["err_password_short"]; return; }
        if (Next != Repeat) { Error = Loc.Instance["err_password_mismatch"]; return; }

        IsBusy = true;
        try
        {
            await auth.ChangePasswordAsync(Current, Next);
            Ui.Toast(Loc.Instance["password_changed"]);
            await Shell.Current.GoToAsync("..");
        }
        catch (Refit.ApiException ex)
        {
            Error = (int)ex.StatusCode == 401 ? Loc.Instance["err_wrong_password"] : SyncService.DescribeError(ex);
        }
        catch
        {
            Error = Loc.Instance["err_no_connection"];
        }
        finally
        {
            IsBusy = false;
        }
    }
}
