using Cartex.Domain.Common;

namespace Cartex.Domain.Entities;

public class CartItem : BaseEntity
{
    public long CartId { get; set; }
    public Cart Cart { get; set; } = null!;

    public long VariantId { get; set; }
    public ProductVariant Variant { get; set; } = null!;

    public long? PrepackId { get; set; }
    public Prepack? Prepack { get; set; }

    public decimal Quantity { get; set; }
    public decimal? UnitPriceOverride { get; set; }
}
