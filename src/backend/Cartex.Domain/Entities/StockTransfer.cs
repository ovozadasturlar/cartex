using Cartex.Domain.Common;
using Cartex.Domain.Enums;

namespace Cartex.Domain.Entities;

public class StockTransfer : AuditableEntity
{
    public long FromWarehouseId { get; set; }
    public Warehouse FromWarehouse { get; set; } = null!;

    public long ToWarehouseId { get; set; }
    public Warehouse ToWarehouse { get; set; } = null!;

    public long ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public decimal Quantity { get; set; }

    public long UserId { get; set; }
    public User User { get; set; } = null!;

    public TransferStatus Status { get; set; } = TransferStatus.Sent;
}
