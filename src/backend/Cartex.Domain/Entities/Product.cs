using Cartex.Domain.Common;

namespace Cartex.Domain.Entities;

public class Product : SoftDeleteEntity
{
    public long? CategoryId { get; set; }
    public Category? Category { get; set; }

    public long UnitId { get; set; }
    public Unit Unit { get; set; } = null!;

    public long? ProductTypeId { get; set; }
    public ProductType? ProductType { get; set; }

    public string Name { get; set; } = null!;
    public decimal MinStock { get; set; }
    public bool? TracksExpiryOverride { get; set; }
    public string? Attributes { get; set; }

    public ICollection<Barcode> Barcodes { get; set; } = [];
    public ICollection<Stock> Stocks { get; set; } = [];
    public ICollection<ProductPrice> Prices { get; set; } = [];
}
