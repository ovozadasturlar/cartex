using Cartex.Domain.Common;
using Cartex.Domain.Enums;

namespace Cartex.Domain.Entities;

public class Prepack : SoftDeleteEntity, IBranchScoped
{
    public long BranchId { get; set; }

    public long WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;

    public long VariantId { get; set; }
    public ProductVariant Variant { get; set; } = null!;

    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string LabelCode { get; set; } = null!;
    public PrepackStatus Status { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public long? SoldSaleId { get; set; }
}
