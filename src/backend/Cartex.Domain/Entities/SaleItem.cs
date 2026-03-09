using Cartex.Domain.Common;

namespace Cartex.Domain.Entities;

public class SaleItem : BaseEntity
{
    public long SaleId { get; set; }
    public Sale Sale { get; set; } = null!;

    public long ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public long StockId { get; set; }
    public Stock Stock { get; set; } = null!;

    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal PurchasePrice { get; set; }
}
