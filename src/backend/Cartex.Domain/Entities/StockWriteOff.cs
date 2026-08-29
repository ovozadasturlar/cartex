using Cartex.Domain.Common;
using Cartex.Domain.Enums;

namespace Cartex.Domain.Entities;

public class StockWriteOffDocument : AuditableEntity, IBranchScoped
{
    public long BranchId { get; set; }
    public long WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;
    public long UserId { get; set; }
    public User User { get; set; } = null!;
    public string DocumentNumber { get; set; } = null!;
    public DateOnly BusinessDate { get; set; }
    public decimal TotalCost { get; set; }
    public long? ReversesDocumentId { get; set; }
    public StockWriteOffDocument? ReversesDocument { get; set; }
    public string? Note { get; set; }
    public string? IdempotencyKey { get; set; }

    public ICollection<StockWriteOffLine> Lines { get; set; } = [];
    public ICollection<Transaction> Transactions { get; set; } = [];
}

public class StockWriteOffLine : BaseEntity
{
    public long StockWriteOffDocumentId { get; set; }
    public StockWriteOffDocument Document { get; set; } = null!;
    public long VariantId { get; set; }
    public ProductVariant Variant { get; set; } = null!;
    public long StockId { get; set; }
    public Stock Stock { get; set; } = null!;
    public long? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal LineCost { get; set; }
    public string? ClaimCurrency { get; set; }
    public decimal ClaimRate { get; set; } = 1m;
    public StockWriteOffReason Reason { get; set; }
    public InventoryDisposition Disposition { get; set; }
    public string? Note { get; set; }
}
