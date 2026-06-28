using Cartex.Domain.Common;
using Cartex.Domain.Enums;

namespace Cartex.Domain.Entities;

public class Sale : SoftDeleteEntity, IBranchScoped
{
    public long BranchId { get; set; }

    public long WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;

    public long UserId { get; set; }
    public User User { get; set; } = null!;

    public long? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public decimal TotalAmount { get; set; }
    public decimal PaidCash { get; set; }
    public decimal PaidCard { get; set; }
    public decimal PaidBonus { get; set; }
    public decimal DebtAmount { get; set; }
    public SaleStatus Status { get; set; } = SaleStatus.Completed;

    public ICollection<SaleItem> Items { get; set; } = [];
}
