using System.Collections.ObjectModel;
using Cartex.Mobile.Agent.Data;
using Cartex.Mobile.Agent.Services;
using Cartex.Mobile.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cartex.Mobile.Agent.ViewModels;

public partial class CatalogViewModel(AgentDb db, SyncService sync, CartService cart, AppCapabilities caps) : ObservableObject
{
    private List<LocalVanStock> _all = [];
    private string _currency = "";
    private CancellationTokenSource? _searchCts;

    public ObservableCollection<CategoryChip> Categories { get; } = [];

    [ObservableProperty] private List<ProductCard> _items = [];
    [ObservableProperty] private string _search = "";
    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] private bool _isLoading = true;
    [ObservableProperty] private long? _categoryId;
    [ObservableProperty] private bool _isGrid = Preferences.Get("catalog_grid", true);
    [ObservableProperty] private string _resultText = "";

    public bool CanManageProducts => caps.CanManageProducts;
    public bool IsEmpty => !IsLoading && Items.Count == 0;
    public CartService Cart => cart;

    partial void OnSearchChanged(string value) => Debounce();
    partial void OnCategoryIdChanged(long? value) => Apply();
    partial void OnIsGridChanged(bool value) => Preferences.Set("catalog_grid", value);

    public async Task AppearAsync()
    {
        if (_all.Count == 0) await LoadAsync();
        cart.Refresh();
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        _currency = await db.GetMetaAsync("base_currency") ?? "";
        _all = await db.GetVanStockAsync();
        BuildCategories();
        Apply();
        IsLoading = false;
    }

    private void BuildCategories()
    {
        var groups = _all
            .Where(s => s.CategoryId is not null && !string.IsNullOrWhiteSpace(s.CategoryName))
            .GroupBy(s => (s.CategoryId!.Value, s.CategoryName!))
            .Select(g => new CategoryChip(g.Key.Item1, g.Key.Item2, g.Count()))
            .OrderByDescending(c => c.Count)
            .Take(24)
            .ToList();

        Categories.Clear();
        Categories.Add(new CategoryChip(null, Loc.Instance["all"], _all.Count));
        foreach (var g in groups) Categories.Add(g);
    }

    private void Debounce()
    {
        _searchCts?.Cancel();
        var cts = _searchCts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(220, cts.Token);
                MainThread.BeginInvokeOnMainThread(Apply);
            }
            catch (TaskCanceledException) { }
        });
    }

    private void Apply()
    {
        IEnumerable<LocalVanStock> query = _all;

        if (CategoryId is { } id)
            query = query.Where(s => s.CategoryId == id);

        var text = Search.Trim();
        if (text.Length > 0)
            query = query.Where(s =>
                s.ProductName.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                (s.Code?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false));

        var list = query
            .OrderByDescending(s => s.Quantity > 0)
            .ThenBy(s => s.ProductName)
            .Select(s => new ProductCard(s, _currency))
            .ToList();

        Items = list;
        ResultText = string.Format(Loc.Instance["stock_total_fmt"], list.Count);
        OnPropertyChanged(nameof(IsEmpty));
    }

    [RelayCommand]
    private void SelectCategory(CategoryChip chip)
    {
        CategoryId = chip.Id;
        foreach (var c in Categories) c.IsSelected = c.Id == chip.Id;
    }

    [RelayCommand]
    private void ToggleView() => IsGrid = !IsGrid;

    [RelayCommand]
    private void ClearSearch() => Search = "";

    [RelayCommand]
    private void Add(ProductCard card)
    {
        cart.Add(card.Stock, 1);
        Ui.Haptic();
    }

    [RelayCommand]
    private async Task OpenAsync(ProductCard card) =>
        await Shell.Current.GoToAsync($"product?variantId={card.Stock.VariantId}");

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await sync.SyncAsync();
        await LoadAsync();
        IsRefreshing = false;
    }

    [RelayCommand]
    private async Task OpenCartAsync() => await Shell.Current.GoToAsync("//cart");

    [RelayCommand]
    private async Task ScanAsync() => await Shell.Current.GoToAsync("scan");
}

public partial class CategoryChip(long? id, string name, int count) : ObservableObject
{
    public long? Id { get; } = id;
    public string Name { get; } = name;
    public int Count { get; } = count;
    [ObservableProperty] private bool _isSelected = id is null;
}

public sealed record ProductCard(LocalVanStock Stock, string Currency)
{
    public string Name => Stock.ProductName;
    public string? ImageUrl => Stock.ImageUrl;
    public string PriceText => Money.Text(Stock.SellingPrice, Currency);
    public string QtyText => Money.QuantityWithUnit(Stock.Quantity, Stock.UnitName);
    public string? Code => Stock.Code;
    public bool HasCode => !string.IsNullOrWhiteSpace(Stock.Code);
    public bool IsOut => Stock.Quantity <= 0;
    public bool InStock => Stock.Quantity > 0;
    public bool HasDiscount => Stock.DiscountPct is > 0;
    public string DiscountText => Stock.DiscountPct is { } d ? $"-{d:0.#}%" : "";
}
