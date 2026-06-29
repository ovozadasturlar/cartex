using Cartex.Domain.Common;

namespace Cartex.Domain.Entities;

public class ProductVariant : SoftDeleteEntity
{
    public long ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public string? Name { get; set; }
    public string? Code { get; set; }
    public string? Attributes { get; set; }
    public string? ImageKey { get; set; }
    public bool IsDefault { get; set; }

    public ICollection<Barcode> Barcodes { get; set; } = [];
    public ICollection<Stock> Stocks { get; set; } = [];
    public ICollection<ProductPrice> Prices { get; set; } = [];
}
