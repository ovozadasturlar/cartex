using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Ordering;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public partial class OrdersViewModel : ViewModelBase, ILoadable
{
    private readonly IOrderingApi _api;
    private readonly PosHandoffService _handoff;
    private readonly NavigationService _navigation;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;

    public OrdersViewModel(IOrderingApi api, PosHandoffService handoff, NavigationService navigation, IToastService toast, IBusyService busy, AuthService auth)
    {
        _api = api;
        _handoff = handoff;
        _navigation = navigation;
        _toast = toast;
        _busy = busy;
        auth.LoggedOut += ResetState;
    }

    public ObservableCollection<CartListDto> Carts { get; } = [];

    [ObservableProperty] private string _statusFilter = "Open";

    public string[] StatusFilters { get; } = ["Open", "Confirmed", "Ready", "CheckedOut", "Cancelled"];
    public bool IsEmpty => Carts.Count == 0;

    private void ResetState()
    {
        Carts.Clear();
        _statusFilter = "Open";
        OnPropertyChanged(nameof(StatusFilter));
        OnPropertyChanged(nameof(IsEmpty));
    }

    public async Task LoadAsync()
    {
        try
        {
            using (_busy.Begin(L["loading"]))
            {
                var carts = await _api.GetAllAsync(StatusFilter, kind: "Order");
                Carts.Clear();
                foreach (var cart in carts) Carts.Add(cart);
                OnPropertyChanged(nameof(IsEmpty));
            }
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    partial void OnStatusFilterChanged(string value) => _ = LoadAsync();

    private async Task SetStatusAsync(CartListDto cart, string status)
    {
        try
        {
            await _api.UpdateStatusAsync(cart.AggregateCode, new UpdateCartStatusRequest(status));
            _toast.Success(L["success"]);
            await LoadAsync();
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private Task Confirm(CartListDto cart) => SetStatusAsync(cart, "Confirmed");

    [RelayCommand]
    private Task MarkReady(CartListDto cart) => SetStatusAsync(cart, "Ready");

    [RelayCommand]
    private Task Cancel(CartListDto cart) => SetStatusAsync(cart, "Cancelled");

    [RelayCommand]
    private void TakeToPos(CartListDto cart)
    {
        _handoff.PendingCartCode = cart.AggregateCode;
        _navigation.RequestMenuNavigation("pos");
    }
}
