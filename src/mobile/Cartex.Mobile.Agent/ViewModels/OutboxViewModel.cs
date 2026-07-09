using System.Collections.ObjectModel;
using Cartex.Mobile.Agent.Data;
using Cartex.Mobile.Agent.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cartex.Mobile.Agent.ViewModels;

public partial class OutboxViewModel(AgentDb db, SyncService sync, SessionStore session) : ObservableObject
{
    public ObservableCollection<OutboxRow> Items { get; } = [];

    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] private bool _isEmpty;

    public async Task AppearAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        Items.Clear();
        foreach (var item in await db.GetOutboxAsync())
            Items.Add(new OutboxRow(item));
        IsEmpty = Items.Count == 0;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await sync.SyncAsync();
        await LoadAsync();
        IsRefreshing = false;
    }

    [RelayCommand]
    private async Task RetryAsync(OutboxRow row)
    {
        await sync.RetryAsync(row.Item);
        await LoadAsync();
    }

    [RelayCommand]
    private async Task DeleteAsync(OutboxRow row)
    {
        var ok = await Shell.Current.CurrentPage.DisplayAlert(Loc.Instance["delete_title"],
            Loc.Instance["delete_msg"], Loc.Instance["yes"], Loc.Instance["no"]);
        if (!ok) return;
        await sync.DeleteAsync(row.Item);
        await LoadAsync();
    }

    [RelayCommand]
    private async Task ShareReceiptAsync(OutboxRow row)
    {
        if (string.IsNullOrEmpty(row.Item.ReceiptToken)) return;
        var url = $"{session.ServerUrl}/r/{row.Item.ReceiptToken}";
        await Share.Default.RequestAsync(new ShareTextRequest(url, Loc.Instance["receipt"]));
    }
}

public sealed record OutboxRow(OutboxItem Item)
{
    public string Title => Loc.Instance[Item.Kind switch
    {
        "sale" => "kind_sale",
        "repay" => "kind_repay",
        "cart" => "kind_order",
        "checkout" => "kind_delivery",
        _ => "kind_sale"
    }];
    public string SubLine => Item.CreatedAt.ToString("dd.MM HH:mm");
    public string StatusText => Item.Status switch
    {
        "pending" => Loc.Instance["status_pending"],
        "done" => Loc.Instance["status_done"],
        _ => Item.Error ?? Loc.Instance["status_error"]
    };
    public bool IsError => Item.Status == "error";
    public bool IsPending => Item.Status == "pending";
    public bool IsDone => Item.Status == "done";
    public bool HasReceipt => Item.Status == "done" && !string.IsNullOrEmpty(Item.ReceiptToken);
}
