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
    public string PriceCurrency { get; set; } = "UZS";
    public decimal PriceRate { get; set; } = 1m;
    public decimal PurchasePrice { get; set; }
    public decimal CashbackEarned { get; set; }
    public decimal ReturnedQuantity { get; set; }
    public decimal ReturnedCashback { get; set; }
}
