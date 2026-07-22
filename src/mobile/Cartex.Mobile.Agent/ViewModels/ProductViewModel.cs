using Cartex.Mobile.Agent.Data;
using Cartex.Mobile.Agent.Services;
using Cartex.Mobile.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cartex.Mobile.Agent.ViewModels;

public partial class ProductViewModel(AgentDb db, CartService cart, AppCapabilities caps) : ObservableObject, IQueryAttributable
{
    private long _variantId;
    private string _currency = "";

    [ObservableProperty] private LocalVanStock? _stock;
    [ObservableProperty] private decimal _quantity = 1;

    public CartService Cart => cart;
    public bool CanManageProducts => caps.CanManageProducts;

    public string Name => Stock?.ProductName ?? "";
    public string? ImageUrl => Stock?.ImageUrl;
    public string PriceText => Stock is null ? "" : Money.Text(Stock.SellingPrice, _currency);
    public string StockText => Stock is null ? "" : Money.QuantityWithUnit(Stock.Quantity, Stock.UnitName);
    public string? CategoryName => Stock?.CategoryName;
    public string? Code => Stock?.Code;
    public string? Dimension => string.IsNullOrWhiteSpace(Stock?.Dimension) ? null : Stock!.Dimension;
    public bool HasCategory => !string.IsNullOrWhiteSpace(CategoryName);
    public bool HasCode => !string.IsNullOrWhiteSpace(Code);
    public bool HasDimension => Dimension is not null;
    public bool IsOut => Stock is { Quantity: <= 0 };
    public string LineTotalText => Stock is null ? "" : Money.Text(Quantity * Stock.SellingPrice, _currency);

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("variantId", out var id))
            _variantId = long.TryParse(Convert.ToString(id), out var parsed) ? parsed : 0;
    }

    public async Task AppearAsync()
    {
        _currency = await db.GetMetaAsync("base_currency") ?? "";
        Stock = (await db.GetVanStockAsync()).FirstOrDefault(s => s.VariantId == _variantId);
        foreach (var name in (string[])[nameof(Name), nameof(ImageUrl), nameof(PriceText), nameof(StockText),
                     nameof(CategoryName), nameof(Code), nameof(Dimension), nameof(HasCategory), nameof(HasCode),
                     nameof(HasDimension), nameof(IsOut), nameof(LineTotalText)])
            OnPropertyChanged(name);
    }

    partial void OnQuantityChanged(decimal value) => OnPropertyChanged(nameof(LineTotalText));

    [RelayCommand]
    private async Task AddAsync()
    {
        if (Stock is null || Quantity <= 0) return;
        cart.Add(Stock, Quantity);
        Ui.Haptic();
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task OpenCartAsync() => await Shell.Current.GoToAsync("//cart");

    [RelayCommand]
    private async Task EditAsync() => await Shell.Current.GoToAsync($"product-edit?variantId={_variantId}");
}
