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
    public decimal QuantityStep { get; set; } = 1;
    [ObservableProperty] private decimal _quantity;
    [ObservableProperty] private bool _isSwiped;
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
                ImageKey = l.ImageKey,
                QuantityStep = l.QuantityStep > 0 ? l.QuantityStep : 1
            }));
        }
        catch
        {
            Preferences.Remove(Key);
        }
    }

    public void Add(ProductLookupDto product)
    {
        var line = Lines.FirstOrDefault(l => l.VariantId == product.VariantId);
        if (line is null)
            Lines.Add(new SupplyCartLine
            {
                VariantId = product.VariantId,
                ProductName = product.ProductName,
                UnitName = product.UnitName,
                Quantity = product.PackQty,
                ImageKey = product.ImageKey,
                QuantityStep = product.PackQty > 1 ? product.PackQty : product.QuantityStep
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
        Save();
    }

    public void Remove(long variantId)
    {
        Lines.RemoveAll(l => l.VariantId == variantId);
        Save();
    }

    public void Clear()
    {
        Lines.Clear();
        Preferences.Remove(Key);
        Changed?.Invoke();
    }

    private void Save()
    {
        var draft = Lines.Select(l => new DraftLine(l.VariantId, l.ProductName, l.UnitName, l.Quantity, l.ImageKey, l.QuantityStep)).ToList();
        Preferences.Set(Key, JsonSerializer.Serialize(draft));
        Changed?.Invoke();
    }

    private sealed record DraftLine(long VariantId, string ProductName, string UnitName, decimal Quantity, string? ImageKey = null, decimal QuantityStep = 1);
}
