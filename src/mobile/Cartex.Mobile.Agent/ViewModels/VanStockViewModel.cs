using System.Collections.ObjectModel;
using Cartex.Mobile.Agent.Data;
using Cartex.Mobile.Agent.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.Mobile.Core;

namespace Cartex.Mobile.Agent.ViewModels;

public partial class VanStockViewModel(AgentDb db, SyncService sync) : ObservableObject
{
    public ObservableCollection<StockRow> Items { get; } = [];

    [ObservableProperty] private string _search = "";
    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] private string _totalText = "";

    private string _currency = "";

    partial void OnSearchChanged(string value) => _ = LoadAsync();

    public async Task AppearAsync()
    {
        _currency = await db.GetMetaAsync("base_currency") ?? "";
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var stock = await db.SearchVanStockAsync(Search);

        Items.Clear();
        foreach (var s in stock)
            Items.Add(new StockRow(s, _currency));
        TotalText = string.Format(Loc.Instance["stock_total_fmt"], Items.Count);
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await sync.SyncAsync();
        await LoadAsync();
        IsRefreshing = false;
    }
}

public sealed record StockRow(LocalVanStock Stock, string Currency)
{
    public string Name => Stock.ProductName;
    public string QtyText => $"{Stock.Quantity:0.###} {Stock.UnitName}";
    public string PriceText => $"{Stock.SellingPrice:N0} {Currency}";
    public bool IsOut => Stock.Quantity <= 0;
}
