using System.Collections.ObjectModel;
using System.Text.Json;
using Cartex.Mobile.Agent.Data;
using Cartex.Mobile.Core;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Cartex.Mobile.Agent.Services;

public partial class CartService : ObservableObject
{
    private const string StorageKey = "cart_lines_v1";

    public ObservableCollection<CartLine> Lines { get; } = [];

    [ObservableProperty] private string _currency = "";
    [ObservableProperty] private long? _customerId;
    [ObservableProperty] private string? _customerName;

    public decimal Total => Lines.Sum(l => l.LineTotal);
    public int Count => Lines.Count;
    public bool IsEmpty => Lines.Count == 0;
    public string TotalText => Money.Text(Total, Currency);
    public string CountText => string.Format(Loc.Instance["cart_count_fmt"], Lines.Count);

    public CartService()
    {
        Lines.CollectionChanged += (_, e) =>
        {
            foreach (var line in e.OldItems?.Cast<CartLine>() ?? [])
                line.PropertyChanged -= OnLineChanged;
            foreach (var line in e.NewItems?.Cast<CartLine>() ?? [])
                line.PropertyChanged += OnLineChanged;
            Refresh();
        };
        Restore();
    }

    private void OnLineChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => Refresh();

    public void Add(LocalVanStock stock, decimal quantity)
    {
        var existing = Lines.FirstOrDefault(l => l.VariantId == stock.VariantId);
        if (existing is not null)
        {
            existing.Quantity += quantity;
            Refresh();
            return;
        }

        Lines.Add(new CartLine
        {
            VariantId = stock.VariantId,
            Name = stock.ProductName,
            UnitName = stock.UnitName,
            ImageUrl = stock.ImageUrl,
            UnitPrice = stock.SellingPrice,
            Available = stock.Quantity,
            Quantity = quantity,
        });
    }

    public void Remove(CartLine line) => Lines.Remove(line);

    public void Clear()
    {
        Lines.Clear();
        CustomerId = null;
        CustomerName = null;
        Refresh();
    }

    public void Refresh()
    {
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(TotalText));
        OnPropertyChanged(nameof(CountText));
        Persist();
    }

    private void Persist()
    {
        try
        {
            Preferences.Set(StorageKey, JsonSerializer.Serialize(Lines.ToList()));
        }
        catch
        {
        }
    }

    private void Restore()
    {
        try
        {
            var json = Preferences.Get(StorageKey, "");
            if (string.IsNullOrWhiteSpace(json)) return;
            foreach (var line in JsonSerializer.Deserialize<List<CartLine>>(json) ?? [])
                Lines.Add(line);
        }
        catch
        {
        }
    }
}

public partial class CartLine : ObservableObject
{
    public long VariantId { get; set; }
    public string Name { get; set; } = "";
    public string UnitName { get; set; } = "";
    public string? ImageUrl { get; set; }
    public decimal Available { get; set; }

    [ObservableProperty] private decimal _quantity = 1;
    [ObservableProperty] private decimal _unitPrice;

    public decimal LineTotal => Quantity * UnitPrice;
    public bool IsOverStock => Quantity > Available;

    partial void OnQuantityChanged(decimal value)
    {
        OnPropertyChanged(nameof(LineTotal));
        OnPropertyChanged(nameof(IsOverStock));
        OnPropertyChanged(nameof(LineTotalText));
    }

    partial void OnUnitPriceChanged(decimal value)
    {
        OnPropertyChanged(nameof(LineTotal));
        OnPropertyChanged(nameof(LineTotalText));
        OnPropertyChanged(nameof(PriceText));
    }

    public string PriceText => Money.Text(UnitPrice);
    public string LineTotalText => Money.Text(LineTotal);
}
