using System.Text.Json;
using Cartex.Shared.Models.Customers;
using Cartex.UI.ViewModels;

namespace Cartex.UI.Services;

public interface IHeldSaleStore
{
    List<HeldSale> Load();
    void Save(IEnumerable<HeldSale> sales);
}

public sealed class HeldSaleStore : IHeldSaleStore
{
    public static TimeSpan Ttl { get; set; } = TimeSpan.FromHours(24);

    private readonly string? _path;

    private sealed record StoredItem(long VariantId, string ProductName, decimal UnitPrice, decimal OriginalPrice, decimal Quantity);
    private sealed record StoredHold(string Label, List<StoredItem> Items, decimal PaidCash, decimal PaidCard, decimal PaidBonus, CustomerDto? Customer, DateTime HeldAt);

    public HeldSaleStore()
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Cartex");
            Directory.CreateDirectory(dir);
            _path = Path.Combine(dir, "held-sales.json");
        }
        catch { _path = null; }
    }

    public List<HeldSale> Load()
    {
        if (_path is null || !File.Exists(_path)) return [];
        try
        {
            var stored = JsonSerializer.Deserialize<List<StoredHold>>(File.ReadAllText(_path)) ?? [];
            var cutoff = DateTime.Now - Ttl;
            var valid = stored.Where(s => s.HeldAt >= cutoff).ToList();

            var result = valid.Select(s => new HeldSale(
                s.Label,
                s.Items.Select(i => new CartItem { VariantId = i.VariantId, ProductName = i.ProductName, UnitPrice = i.UnitPrice, OriginalPrice = i.OriginalPrice, Quantity = i.Quantity }).ToList(),
                s.PaidCash, s.PaidCard, s.PaidBonus, s.Customer, s.HeldAt)).ToList();

            if (valid.Count != stored.Count) Persist(valid);
            return result;
        }
        catch { return []; }
    }

    public void Save(IEnumerable<HeldSale> sales) =>
        Persist(sales.Select(s => new StoredHold(
            s.Label,
            s.Items.Select(i => new StoredItem(i.VariantId, i.ProductName, i.UnitPrice, i.OriginalPrice, i.Quantity)).ToList(),
            s.PaidCash, s.PaidCard, s.PaidBonus, s.Customer, s.HeldAt)).ToList());

    private void Persist(IEnumerable<StoredHold> holds)
    {
        if (_path is null) return;
        try { File.WriteAllText(_path, JsonSerializer.Serialize(holds)); }
        catch { }
    }
}
