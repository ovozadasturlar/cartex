using Cartex.Domain.Common;

namespace Cartex.Domain.Entities;

public class StockAdjustment : SoftDeleteEntity, IBranchScoped
{
    public long BranchId { get; set; }
    public long WarehouseId { get; set; }
    public long VariantId { get; set; }
    public long? StockId { get; set; }
    public decimal SystemQuantity { get; set; }
    public decimal CountedQuantity { get; set; }
    public decimal Difference { get; set; }
    public string? Reason { get; set; }
    public long UserId { get; set; }
}
