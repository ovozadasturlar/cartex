using System.Collections.ObjectModel;
using Cartex.ApiClient.Api;
using Cartex.Mobile.Agent.Data;
using Cartex.Mobile.Agent.Services;
using Cartex.Shared.Models.StockTransfers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Refit;
using Cartex.Mobile.Core;

namespace Cartex.Mobile.Agent.ViewModels;

public partial class TransfersViewModel(IStockTransfersApi api, AgentDb db, SyncService sync) : ObservableObject
{
    public ObservableCollection<TransferRow> Transfers { get; } = [];

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private bool _isEmpty;

    public async Task AppearAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        Error = null;
        if (!long.TryParse(await db.GetMetaAsync("warehouse_id"), out var warehouseId) || warehouseId == 0)
        {
            Error = Loc.Instance["err_no_warehouse"];
            return;
        }
        IsBusy = true;
        try
        {
            var today = DateTime.Today;
            var items = await api.GetAllAsync(fromDate: today, toDate: today.AddDays(1), toWarehouseId: warehouseId);
            Transfers.Clear();
            foreach (var t in items)
                Transfers.Add(new TransferRow(t));
            IsEmpty = Transfers.Count == 0;
        }
        catch (ApiException ex)
        {
            Error = SyncService.DescribeError(ex);
        }
        catch
        {
            Error = Loc.Instance["transfers_offline"];
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadAsync();
        IsRefreshing = false;
    }

    [RelayCommand]
    private async Task ReceiveAsync(TransferRow row)
    {
        try
        {
            await api.ReceiveAsync(row.Transfer.Id);
            Ui.Toast(Loc.Instance["received_toast"]);
            await sync.SyncAsync();
            await LoadAsync();
        }
        catch (Exception ex)
        {
            Ui.Toast(ex is ApiException api2 ? SyncService.DescribeError(api2) : Loc.Instance["err_no_connection"]);
        }
    }
}

public sealed record TransferRow(StockTransferDto Transfer)
{
    public string Line => $"{Transfer.ProductName} — {Transfer.Quantity:0.###}";
    public string SubLine => $"{Transfer.FromWarehouse} → {Transfer.ToWarehouse} • {Transfer.CreatedAt.ToLocalTime():HH:mm}";
    public bool CanReceive => Transfer.Status == "Sent";
    public string StatusText => Transfer.Status switch
    {
        "Sent" => Loc.Instance["status_sent"],
        "Received" => Loc.Instance["status_received"],
        "Cancelled" => Loc.Instance["status_cancelled"],
        _ => Transfer.Status
    };
}
