using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Ordering;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public partial class OrdersViewModel(IOrderingApi api, PosHandoffService handoff, NavigationService navigation, IToastService toast, IBusyService busy) : ViewModelBase, ILoadable
{
    public ObservableCollection<CartListDto> Carts { get; } = [];

    [ObservableProperty] private string _statusFilter = "Open";

    public string[] StatusFilters { get; } = ["Open", "Confirmed", "Ready", "CheckedOut", "Cancelled"];
    public bool IsEmpty => Carts.Count == 0;

    public async Task LoadAsync()
    {
        try
        {
            using (busy.Begin(L["loading"]))
            {
                var carts = await api.GetAllAsync(StatusFilter);
                Carts.Clear();
                foreach (var cart in carts) Carts.Add(cart);
                OnPropertyChanged(nameof(IsEmpty));
            }
        }
        catch (Exception ex) { toast.Error(ApiErrors.Describe(ex)); }
    }

    partial void OnStatusFilterChanged(string value) => _ = LoadAsync();

    private async Task SetStatusAsync(CartListDto cart, string status)
    {
        try
        {
            await api.UpdateStatusAsync(cart.AggregateCode, new UpdateCartStatusRequest(status));
            toast.Success(L["success"]);
            await LoadAsync();
        }
        catch (Exception ex) { toast.Error(ApiErrors.Describe(ex)); }
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
        handoff.PendingCartCode = cart.AggregateCode;
        navigation.RequestMenuNavigation("pos");
    }
}
