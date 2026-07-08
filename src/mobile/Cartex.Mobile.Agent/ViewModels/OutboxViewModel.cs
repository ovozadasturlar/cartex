using System.Collections.ObjectModel;
using Cartex.Mobile.Agent.Data;
using Cartex.Mobile.Agent.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cartex.Mobile.Agent.ViewModels;

public partial class OutboxViewModel(AgentDb db, SyncService sync) : ObservableObject
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
        var ok = await Shell.Current.CurrentPage.DisplayAlert("O'chirish",
            "Bu amal serverga yuborilmaydi. O'chirilsinmi?", "Ha", "Yo'q");
        if (!ok) return;
        await db.DeleteOutboxAsync(row.Item.Id);
        await sync.SyncAsync();
        await LoadAsync();
    }
}

public sealed record OutboxRow(OutboxItem Item)
{
    public string Title => Item.Kind == "sale" ? "Savdo" : "Qarz to'lovi";
    public string SubLine => Item.CreatedAt.ToString("dd.MM HH:mm");
    public string StatusText => Item.Status switch
    {
        "pending" => "Kutilmoqda",
        "done" => "Yuborildi",
        _ => Item.Error ?? "Xato"
    };
    public bool IsError => Item.Status == "error";
    public bool IsPending => Item.Status == "pending";
    public bool IsDone => Item.Status == "done";
}
