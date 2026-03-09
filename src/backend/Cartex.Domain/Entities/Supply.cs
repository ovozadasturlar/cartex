using Cartex.Domain.Common;

namespace Cartex.Domain.Entities;

public class Supply : AuditableEntity
{
    public long SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;

    public long WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;

    public long UserId { get; set; }
    public User User { get; set; } = null!;

    public decimal TotalAmount { get; set; }
    public DateOnly SupplyDate { get; set; }

    public ICollection<SupplyItem> Items { get; set; } = [];
}
