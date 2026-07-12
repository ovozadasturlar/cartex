using System.Text.Json;
using Cartex.Shared.Models.Products;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Cartex.Mobile.Store.Services;

public sealed partial class CartLine : ObservableObject
{
    public long VariantId { get; set; }
    public string ProductName { get; set; } = "";
    public string UnitName { get; set; } = "";
    public decimal UnitPrice { get; set; }
    [ObservableProperty] private decimal _quantity;
    public decimal LineTotal => UnitPrice * Quantity;

    partial void OnQuantityChanged(decimal value) => OnPropertyChanged(nameof(LineTotal));
}

public sealed class CartStore
{
    private const string Key = "cart_draft";

    public List<CartLine> Lines { get; } = [];
    public long? CustomerId { get; private set; }
    public string? CustomerName { get; private set; }
    public string Note { get; private set; } = "";

    public event Action? Changed;

    public int Count => Lines.Count;
    public decimal Total => Lines.Sum(l => l.LineTotal);

    public CartStore()
    {
        var json = Preferences.Get(Key, "");
        if (string.IsNullOrEmpty(json)) return;
        try
        {
            var draft = JsonSerializer.Deserialize<Draft>(json);
            if (draft is null) return;
            Lines.AddRange(draft.Lines.Select(l => new CartLine
            {
                VariantId = l.VariantId,
                ProductName = l.ProductName,
                UnitName = l.UnitName,
                UnitPrice = l.UnitPrice,
                Quantity = l.Quantity
            }));
            CustomerId = draft.CustomerId;
            CustomerName = draft.CustomerName;
            Note = draft.Note ?? "";
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
            Lines.Add(new CartLine
            {
                VariantId = product.VariantId,
                ProductName = product.ProductName,
                UnitName = product.UnitName,
                UnitPrice = product.SellingPrice,
                Quantity = product.PackQty
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

    public void SetCustomer(long? id, string? name)
    {
        CustomerId = id;
        CustomerName = name;
        Save();
    }

    public void SetNote(string note)
    {
        Note = note;
        Save();
    }

    public void Clear()
    {
        Lines.Clear();
        CustomerId = null;
        CustomerName = null;
        Note = "";
        Preferences.Remove(Key);
        Changed?.Invoke();
    }

    private void Save()
    {
        var draft = new Draft(
            Lines.Select(l => new DraftLine(l.VariantId, l.ProductName, l.UnitName, l.UnitPrice, l.Quantity)).ToList(),
            CustomerId, CustomerName, Note);
        Preferences.Set(Key, JsonSerializer.Serialize(draft));
        Changed?.Invoke();
    }

    private sealed record DraftLine(long VariantId, string ProductName, string UnitName, decimal UnitPrice, decimal Quantity);
    private sealed record Draft(List<DraftLine> Lines, long? CustomerId, string? CustomerName, string? Note);
}
