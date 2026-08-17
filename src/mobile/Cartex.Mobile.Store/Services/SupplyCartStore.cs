using System.Text.Json;
using Cartex.Shared.Models.Products;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Cartex.Mobile.Store.Services;

public sealed partial class SupplyCartLine : ObservableObject
{
    public long VariantId { get; set; }
    public string ProductName { get; set; } = "";
    public string UnitName { get; set; } = "";
    public string? ImageKey { get; set; }
    [ObservableProperty] private decimal _quantity;
    [ObservableProperty] private decimal _purchasePrice;
    [ObservableProperty] private bool _isSwiped;

    public decimal LineTotal => Quantity * PurchasePrice;

    /// Kiritish uchun alohida matn: raqamli bog'lanish har bosilgan harfda qayta
    /// formatlanib terishni buzadi, shuning uchun xom matn shu yerda turadi.
    public string PriceText
    {
        get => _priceText ??= PurchasePrice > 0 ? PurchasePrice.ToString("0.##") : "";
        set
        {
            _priceText = value;
            PurchasePrice = decimal.TryParse(value?.Replace(" ", "").Replace(',', '.'),
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed) && parsed > 0
                ? parsed
                : 0;
        }
    }

    private string? _priceText;

    partial void OnQuantityChanged(decimal value) => OnPropertyChanged(nameof(LineTotal));
    partial void OnPurchasePriceChanged(decimal value) => OnPropertyChanged(nameof(LineTotal));
}

public sealed class SupplyCartStore
{
    private const string Key = "supply_cart_draft";

    public List<SupplyCartLine> Lines { get; } = [];

    public event Action? Changed;

    public int Count => Lines.Count;

    public SupplyCartStore()
    {
        var json = Preferences.Get(Key, "");
        if (string.IsNullOrEmpty(json)) return;
        try
        {
            var draft = JsonSerializer.Deserialize<List<DraftLine>>(json);
            if (draft is null) return;
            Lines.AddRange(draft.Select(l => new SupplyCartLine
            {
                VariantId = l.VariantId,
                ProductName = l.ProductName,
                UnitName = l.UnitName,
                Quantity = l.Quantity,
                PurchasePrice = l.PurchasePrice,
                ImageKey = l.ImageKey
            }));
        }
        catch
        {
            Preferences.Remove(Key);
        }
    }

    public void Add(ProductLookupDto product, decimal purchasePrice = 0)
    {
        var line = Lines.FirstOrDefault(l => l.VariantId == product.VariantId);
        if (line is null)
            Lines.Add(new SupplyCartLine
            {
                VariantId = product.VariantId,
                ProductName = product.ProductName,
                UnitName = product.UnitName,
                Quantity = product.PackQty,
                PurchasePrice = purchasePrice,
                ImageKey = product.ImageKey
            });
        else
            line.Quantity += product.PackQty;
        Save();
    }

    public void SetQuantity(long variantId, decimal quantity)
    {
        var line = Lines.FirstOrDefault(l => l.VariantId == variantId);
        if (line is null) return;
        if (quantity <= 0) Lines.Remove(line);
        else line.Quantity = quantity;
        Save(debouncePersistence: true);
    }

    public void SetPurchasePrice(long variantId, decimal price)
    {
        if (Lines.FirstOrDefault(l => l.VariantId == variantId) is not { } line) return;
        line.PriceText = price > 0 ? price.ToString("0.##") : "";
        Save(debouncePersistence: true);
    }

    /// Narx qatorning o'zida tahrirlanadi, shuning uchun ro'yxatni qayta qurmasdan
    /// faqat saqlash rejalashtiriladi.
    public void PersistSoon() => SchedulePersistence();

    public void Remove(long variantId)
    {
        Lines.RemoveAll(l => l.VariantId == variantId);
        Save();
    }

    public void Clear()
    {
        Lines.Clear();
        var pending = Interlocked.Exchange(ref _persistCts, null);
        pending?.Cancel();
        pending?.Dispose();
        Preferences.Remove(Key);
        Changed?.Invoke();
    }

    private CancellationTokenSource? _persistCts;

    private void Save(bool debouncePersistence = false)
    {
        Changed?.Invoke();
        if (debouncePersistence)
        {
            SchedulePersistence();
            return;
        }
        PersistNow();
    }

    private void PersistNow()
    {
        var pending = Interlocked.Exchange(ref _persistCts, null);
        pending?.Cancel();
        pending?.Dispose();
        var draft = Lines
            .Select(l => new DraftLine(l.VariantId, l.ProductName, l.UnitName, l.Quantity, l.ImageKey, l.PurchasePrice))
            .ToList();
        Preferences.Set(Key, JsonSerializer.Serialize(draft));
    }

    private void SchedulePersistence()
    {
        var next = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref _persistCts, next);
        previous?.Cancel();
        previous?.Dispose();
        _ = PersistAfterQuietPeriodAsync(next);
    }

    private async Task PersistAfterQuietPeriodAsync(CancellationTokenSource owner)
    {
        try
        {
            await Task.Delay(140, owner.Token);
            if (ReferenceEquals(_persistCts, owner))
                PersistNow();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private sealed record DraftLine(long VariantId, string ProductName, string UnitName, decimal Quantity,
        string? ImageKey = null, decimal PurchasePrice = 0);
}
