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
    public CartStatus Status { get; set; } = CartStatus.Open;

    public ICollection<CartItem> Items { get; set; } = [];
}
