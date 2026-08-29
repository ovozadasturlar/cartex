using Cartex.Domain.Common;

namespace Cartex.Domain.Entities;

public class SaleItem : BaseEntity
{
    public long SaleId { get; set; }
    public Sale Sale { get; set; } = null!;

    public long VariantId { get; set; }
    public ProductVariant Variant { get; set; } = null!;

    public long StockId { get; set; }
    public Stock Stock { get; set; } = null!;

    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal EnteredUnitPrice { get; set; }

    /// Every discount that landed on this row: price cut, automatic rule, manual and rounding.
    /// The row's net is <c>Quantity * UnitPrice - DiscountAmount</c>, and the rows always
    /// add back up to <see cref="Sale.DiscountAmount"/>.
    public decimal DiscountAmount { get; set; }
    public string PriceCurrency { get; set; } = "UZS";
    public decimal PriceRate { get; set; } = 1m;
    public decimal PurchasePrice { get; set; }
    public decimal CashbackEarned { get; set; }
    public decimal ReturnedQuantity { get; set; }
    public decimal ReturnedCashback { get; set; }
}
