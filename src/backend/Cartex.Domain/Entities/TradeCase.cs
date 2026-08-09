using Cartex.Domain.Common;
using Cartex.Domain.Enums;

namespace Cartex.Domain.Entities;

/// <summary>
/// A neutral, configurable business engagement. UI may label it as an object,
/// project, order, contract, job, or account without changing the domain model.
/// </summary>
public sealed class TradeCase : AuditableEntity, IBranchScoped
{
    public long BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public long WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;
    public long CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public string CaseNumber { get; set; } = null!;
    public DateOnly BusinessDate { get; set; }
    public string Title { get; set; } = null!;
    public string? SiteAddress { get; set; }
    public string Currency { get; set; } = "UZS";
    public TradeCaseWorkflow Workflow { get; set; } = TradeCaseWorkflow.CustodyUntilSettlement;
    public TradeCasePricePolicy PricePolicy { get; set; } = TradeCasePricePolicy.SnapshotAtIssue;
    public TradeCaseStatus Status { get; set; } = TradeCaseStatus.Open;
    public int Version { get; set; } = 1;
    public DateTime? SettledAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? Note { get; set; }
    public string? IdempotencyKey { get; set; }

    public ICollection<GoodsIssueDocument> Issues { get; set; } = [];
    public ICollection<GoodsReturnDocument> Returns { get; set; } = [];
    public ICollection<TradeCaseSettlement> Settlements { get; set; } = [];
    public ICollection<Sale> Sales { get; set; } = [];
    public ICollection<CustomerPaymentDocument> Payments { get; set; } = [];
    public ICollection<TradeCaseParticipant> Participants { get; set; } = [];
}

public sealed class GoodsIssueDocument : AuditableEntity, IBranchScoped
{
    public long BranchId { get; set; }
    public long TradeCaseId { get; set; }
    public TradeCase TradeCase { get; set; } = null!;
    public long WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;
    public long CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public long UserId { get; set; }
    public User User { get; set; } = null!;
    public string DocumentNumber { get; set; } = null!;
    public DateOnly BusinessDate { get; set; }
    public BusinessDocumentStatus Status { get; set; } = BusinessDocumentStatus.Posted;
    public decimal EstimatedAmount { get; set; }
    public string Currency { get; set; } = "UZS";
    public string? Note { get; set; }
    public string? IdempotencyKey { get; set; }
    public ICollection<GoodsIssueLine> Lines { get; set; } = [];
}

public sealed class GoodsIssueLine : BaseEntity
{
    public long GoodsIssueDocumentId { get; set; }
    public GoodsIssueDocument Document { get; set; } = null!;
    public long VariantId { get; set; }
    public ProductVariant Variant { get; set; } = null!;
    public long StockId { get; set; }
    public Stock Stock { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal ReturnedQuantity { get; set; }
    public decimal SettledQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string PriceCurrency { get; set; } = "UZS";
    public decimal PriceRate { get; set; } = 1m;
    public decimal PurchasePrice { get; set; }
}

public sealed class GoodsReturnDocument : AuditableEntity, IBranchScoped
{
    public long BranchId { get; set; }
    public long TradeCaseId { get; set; }
    public TradeCase TradeCase { get; set; } = null!;
    public long WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;
    public long CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public long UserId { get; set; }
    public User User { get; set; } = null!;
    public string DocumentNumber { get; set; } = null!;
    public DateOnly BusinessDate { get; set; }
    public BusinessDocumentStatus Status { get; set; } = BusinessDocumentStatus.Posted;
    public string? Note { get; set; }
    public string? IdempotencyKey { get; set; }
    public ICollection<GoodsReturnLine> Lines { get; set; } = [];
}

public sealed class GoodsReturnLine : BaseEntity
{
    public long GoodsReturnDocumentId { get; set; }
    public GoodsReturnDocument Document { get; set; } = null!;
    public long GoodsIssueLineId { get; set; }
    public GoodsIssueLine IssueLine { get; set; } = null!;
    public long VariantId { get; set; }
    public ProductVariant Variant { get; set; } = null!;
    public decimal Quantity { get; set; }
    public string? Reason { get; set; }
    public ReturnItemCondition Condition { get; set; }
    public InventoryDisposition Disposition { get; set; }
}

public sealed class TradeCaseSettlement : AuditableEntity, IBranchScoped
{
    public long BranchId { get; set; }
    public long TradeCaseId { get; set; }
    public TradeCase TradeCase { get; set; } = null!;
    public long SaleId { get; set; }
    public Sale Sale { get; set; } = null!;
    public long UserId { get; set; }
    public User User { get; set; } = null!;
    public string DocumentNumber { get; set; } = null!;
    public DateOnly BusinessDate { get; set; }
    public BusinessDocumentStatus Status { get; set; } = BusinessDocumentStatus.Posted;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "UZS";
    public string? Note { get; set; }
    public string? IdempotencyKey { get; set; }
}
