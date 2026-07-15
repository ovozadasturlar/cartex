using Cartex.Domain.Common;
using Cartex.Domain.Enums;

namespace Cartex.Domain.Entities;

public class SupplyItem : BaseEntity
{
    public long SupplyId { get; set; }
    public Supply Supply { get; set; } = null!;

    public long VariantId { get; set; }
    public ProductVariant Variant { get; set; } = null!;

    public long? UnitId { get; set; }
    public Unit? Unit { get; set; }

    public long? PackId { get; set; }
    public ProductPack? Pack { get; set; }

    public decimal Quantity { get; set; }
    public decimal PurchasePrice { get; set; }

    public decimal PackSize { get; set; } = 1;
    public decimal EntryQuantity { get; set; }
    public decimal EntryPrice { get; set; }
    public SupplyPriceBasis PriceBasis { get; set; } = SupplyPriceBasis.PerEntry;
}
