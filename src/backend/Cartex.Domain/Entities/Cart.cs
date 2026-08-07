using Cartex.Domain.Common;
using Cartex.Domain.Enums;

namespace Cartex.Domain.Entities;

public class Cart : SoftDeleteEntity, IBranchScoped
{
    public long BranchId { get; set; }

    public long WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;

    public long? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public string AggregateCode { get; set; } = null!;
    public string? IdempotencyKey { get; set; }
    public string? Note { get; set; }
    public CartStatus Status { get; set; } = CartStatus.Open;
    public CartKind Kind { get; set; } = CartKind.Queue;

    public decimal PaidCash { get; set; }
    public decimal PaidCard { get; set; }
    public decimal PaidBonus { get; set; }

    public ICollection<CartItem> Items { get; set; } = [];
}
