using Cartex.Domain.Common;

namespace Cartex.Domain.Entities;

public class Stock : SoftDeleteEntity, IBranchScoped
{
    public long BranchId { get; set; }

    public long VariantId { get; set; }
    public ProductVariant Variant { get; set; } = null!;

    public long WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;

    public long? SupplyId { get; set; }
    public Supply? Supply { get; set; }

    public decimal Quantity { get; set; }
    public decimal PurchasePrice { get; set; }
    public DateOnly? ExpiredAt { get; set; }
}
