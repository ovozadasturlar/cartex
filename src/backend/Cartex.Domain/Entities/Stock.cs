using Cartex.Domain.Common;

namespace Cartex.Domain.Entities;

public class Stock : SoftDeleteEntity, IBranchScoped
{
    public long BranchId { get; set; }

    public long ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public long WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;

    public decimal Quantity { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal SellingPrice { get; set; }
    public DateOnly? ExpiredAt { get; set; }
}
