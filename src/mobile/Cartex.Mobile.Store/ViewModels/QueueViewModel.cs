using System.Collections.ObjectModel;
using Cartex.ApiClient.Api;
using Cartex.Mobile.Core;
using Cartex.Mobile.Store.Services;
using Cartex.Shared.Models.Ordering;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Refit;

namespace Cartex.Mobile.Store.ViewModels;

public partial class QueueViewModel(IOrderingApi orderingApi, MobilePermissions permissions) : ObservableObject
{
    public ObservableCollection<QueueRow> Carts { get; } = [];

    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] private string _selectedStatus = "Open";

    public async Task LoadAsync()
    {
        try
        {
            var items = await orderingApi.GetAllAsync(SelectedStatus);
            Carts.Clear();
            foreach (var c in items)
                Carts.Add(new QueueRow(c));
        }
        catch (Exception ex)
        {
            Ui.Toast(ex is ApiException api ? ApiErrors.Describe(api) : Loc.Instance["err_no_connection"]);
        }
    }

    [RelayCommand]
    private async Task SelectStatusAsync(string status)
    {
        SelectedStatus = status;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadAsync();
        IsRefreshing = false;
    }

    [RelayCommand]
    private async Task OpenAsync(QueueRow row)
    {
        if (permissions.Has("sales.create") && row.Cart.Status is "Open" or "Confirmed")
            await Shell.Current.GoToAsync($"checkout?code={row.Cart.AggregateCode}");
        else
            await Shell.Current.GoToAsync($"handoff?code={row.Cart.AggregateCode}");
    }

    [RelayCommand]
    private async Task CancelAsync(QueueRow row)
    {
        var page = Shell.Current.CurrentPage;
        if (!await page.DisplayAlertAsync(Loc.Instance["cart_cancel_title"], Loc.Instance["cart_cancel_confirm"], Loc.Instance["yes"], Loc.Instance["no"]))
            return;
        try
        {
            await orderingApi.UpdateStatusAsync(row.Cart.AggregateCode, new UpdateCartStatusRequest("Cancelled"));
            Ui.Toast(Loc.Instance["cart_cancelled"]);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            Ui.Toast(ex is ApiException api ? ApiErrors.Describe(api) : Loc.Instance["err_no_connection"]);
        }
    }

}

public sealed record QueueRow(CartListDto Cart)
{
    public string CreatedBy => string.IsNullOrEmpty(Cart.CreatedByName) ? "—" : Cart.CreatedByName;
    public string TimeText => Cart.CreatedAt.ToLocalTime().ToString("HH:mm");
    public string CustomerText => string.IsNullOrEmpty(Cart.CustomerName) ? Loc.Instance["no_customer"] : Cart.CustomerName!;
    public string SummaryText => $"{Cart.ItemCount} {Loc.Instance["items_short"]} • {Cart.EstimatedTotal:N0} UZS";
    public string NoteText => Cart.Note ?? "";
    public bool HasNote => !string.IsNullOrEmpty(Cart.Note);
    public bool CanCancel => Cart.Status == "Open";
}
