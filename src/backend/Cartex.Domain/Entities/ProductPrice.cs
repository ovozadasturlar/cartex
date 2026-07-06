using Cartex.Domain.Common;

namespace Cartex.Domain.Entities;

public class ProductPrice : SoftDeleteEntity
{
    public long VariantId { get; set; }
    public ProductVariant Variant { get; set; } = null!;

    public long? WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    public decimal SellingPrice { get; set; }
    public string Currency { get; set; } = "UZS";
}
