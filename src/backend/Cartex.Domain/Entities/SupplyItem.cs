using Cartex.Domain.Common;

namespace Cartex.Domain.Entities;

public class SupplyItem : BaseEntity
{
    public long SupplyId { get; set; }
    public Supply Supply { get; set; } = null!;

    public long ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public decimal Quantity { get; set; }
    public decimal PurchasePrice { get; set; }
}
