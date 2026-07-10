using System.Collections.ObjectModel;
using System.Text.Json;
using Cartex.Mobile.Agent.Data;
using Cartex.Mobile.Agent.Models;
using Cartex.Mobile.Agent.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cartex.Mobile.Agent.ViewModels;

public partial class OrdersViewModel(AgentDb db, SyncService sync) : ObservableObject
{
    public ObservableCollection<OrderRow> Orders { get; } = [];
    public ObservableCollection<LoadRow> Load { get; } = [];

    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] private bool _showLoad;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private int _deliveredCount;
    [ObservableProperty] private int _pendingCount;
    [ObservableProperty] private string _loadKindsText = "";

    private string _currency = "";

    public async Task AppearAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        _currency = await db.GetMetaAsync("base_currency") ?? "";
        var orders = await db.GetOrdersAsync();

        Orders.Clear();
        foreach (var o in orders)
            Orders.Add(new OrderRow(o, _currency));
        TotalCount = orders.Count;
        DeliveredCount = orders.Count(o => o.Status == "delivered");
        PendingCount = TotalCount - DeliveredCount;

        var agg = new Dictionary<long, LoadRow>();
        foreach (var o in orders.Where(o => o.Status != "delivered"))
        {
            var items = JsonSerializer.Deserialize<List<OrderDraftItem>>(o.ItemsJson) ?? [];
            foreach (var it in items)
            {
                if (agg.TryGetValue(it.VariantId, out var row))
                    row.Quantity += it.Quantity;
                else
                    agg[it.VariantId] = new LoadRow { Name = it.Name, UnitName = it.UnitName, Quantity = it.Quantity };
            }
        }
        Load.Clear();
        foreach (var r in agg.Values.OrderBy(r => r.Name))
            Load.Add(r);
        LoadKindsText = $"{Load.Count} {Loc.Instance["load_kinds"]}";
    }

    [RelayCommand]
    private void ShowOrders() => ShowLoad = false;

    [RelayCommand]
    private void ShowLoadList() => ShowLoad = true;

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await sync.SyncAsync();
        await LoadAsync();
        IsRefreshing = false;
    }

    [RelayCommand]
    private Task NewOrderAsync() => Shell.Current.GoToAsync("order");

    [RelayCommand]
    private async Task DeliverAsync(OrderRow row)
    {
        if (row.Order.Status == "delivered") return;
        if (string.IsNullOrEmpty(row.Order.Code))
        {
            Ui.Toast(Loc.Instance["order_not_synced"]);
            return;
        }
        var page = Shell.Current.CurrentPage;
        var input = await page.DisplayPromptAsync(Loc.Instance["deliver_title"], Loc.Instance["paid_cash"],
            Loc.Instance["deliver_btn"], Loc.Instance["cancel"], initialValue: row.Order.Total.ToString("0"), keyboard: Keyboard.Numeric);
        if (input is null) return;
        if (!decimal.TryParse(input.Replace(" ", ""), out var paid) || paid < 0) { Ui.Toast(Loc.Instance["amount_invalid"]); return; }
        if (paid > row.Order.Total) { Ui.Toast(Loc.Instance["err_paid_gt_total"]); return; }
        if (paid < row.Order.Total && row.Order.CustomerId is null) { Ui.Toast(Loc.Instance["err_debt_needs_customer"]); return; }

        var items = JsonSerializer.Deserialize<List<OrderDraftItem>>(row.Order.ItemsJson) ?? [];
        var draft = new CheckoutDraft(row.Order.Code!, row.Order.CustomerId, row.Order.CustomerName, row.Order.Total, paid, items);
        await sync.EnqueueCheckoutAsync(draft);
        Ui.Toast(Loc.Instance["delivered_toast"]);
        await LoadAsync();
    }
}

public sealed record OrderRow(LocalOrder Order, string Currency)
{
    public string Title => Order.CustomerName;
    public string SubLine => $"{Order.CreatedAt:dd.MM HH:mm} • {Order.Total:N0} {Currency}";
    public string StatusText => Loc.Instance[Order.Status switch
    {
        "new" => "order_new",
        "synced" => "order_ready",
        "delivered" => "order_delivered",
        _ => "order_new"
    }];
    public bool CanDeliver => Order.Status != "delivered";
    public bool IsDelivered => Order.Status == "delivered";
}

public sealed class LoadRow
{
    public string Name { get; init; } = "";
    public string UnitName { get; init; } = "";
    public decimal Quantity { get; set; }
    public string QtyText => $"{Quantity:0.###} {UnitName}";
}
