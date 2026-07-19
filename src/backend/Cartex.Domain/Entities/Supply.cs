using Cartex.Domain.Common;

namespace Cartex.Domain.Entities;

public class Supply : SoftDeleteEntity, IBranchScoped
{
    public long BranchId { get; set; }

    public long? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public long WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;

    public long UserId { get; set; }
    public User User { get; set; } = null!;

    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "UZS";
    public decimal Rate { get; set; } = 1m;
    public DateOnly SupplyDate { get; set; }

    public ICollection<SupplyItem> Items { get; set; } = [];
}
