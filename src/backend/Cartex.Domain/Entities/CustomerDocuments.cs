using Cartex.Domain.Common;
using Cartex.Domain.Enums;

namespace Cartex.Domain.Entities;

public class CustomerPaymentDocument : AuditableEntity, IBranchScoped
{
    public long BranchId { get; set; }
    public long CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public long UserId { get; set; }
    public User User { get; set; } = null!;
    public string DocumentNumber { get; set; } = null!;
    public DateOnly BusinessDate { get; set; }
    public BusinessDocumentStatus Status { get; set; } = BusinessDocumentStatus.Posted;
    public decimal TotalBaseAmount { get; set; }
    public decimal AllocatedBaseAmount { get; set; }
    public decimal AdvanceBaseAmount { get; set; }

    /// Debt forgiven with this document. It settles the customer's balance like a payment but
    /// is no money received, so it is kept apart from the tendered total.
    public decimal WriteOffBaseAmount { get; set; }
    public string? WriteOffReason { get; set; }

    /// See CustomerRefundDocument.BalanceAfterBase.
    public decimal BalanceAfterBase { get; set; }

    public string? Note { get; set; }
    public string? IdempotencyKey { get; set; }

    public ICollection<CustomerPaymentTender> Tenders { get; set; } = [];
    public ICollection<CustomerPaymentAllocation> Allocations { get; set; } = [];
    public ICollection<Transaction> Transactions { get; set; } = [];
}

public class CustomerPaymentTender : BaseEntity
{
    public long CustomerPaymentDocumentId { get; set; }
    public CustomerPaymentDocument Document { get; set; } = null!;
    public PaymentMethod Method { get; set; }
    public string Currency { get; set; } = "UZS";
    public decimal Amount { get; set; }
    public decimal Rate { get; set; } = 1m;
    public decimal AmountBase { get; set; }
}

public class CustomerPaymentAllocation : BaseEntity
{
    public long CustomerPaymentDocumentId { get; set; }
    public CustomerPaymentDocument Document { get; set; } = null!;
    public long? SaleId { get; set; }
    public Sale? Sale { get; set; }
    public string Currency { get; set; } = "UZS";
    public decimal Amount { get; set; }
    public decimal Rate { get; set; } = 1m;
    public decimal AmountBase { get; set; }

    /// Both kinds consume the sale's remaining debt, but only a payment is money received —
    /// partner rewards and payment reporting must never count a forgiveness.
    public CustomerPaymentAllocationKind Kind { get; set; } = CustomerPaymentAllocationKind.Payment;
}

public class CustomerRefundDocument : AuditableEntity, IBranchScoped
{
    public long BranchId { get; set; }
    public long CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public long UserId { get; set; }
    public User User { get; set; } = null!;
    public string DocumentNumber { get; set; } = null!;
    public DateOnly BusinessDate { get; set; }
    public BusinessDocumentStatus Status { get; set; } = BusinessDocumentStatus.Posted;
    public decimal TotalBaseAmount { get; set; }

    /// How the payout was funded. The customer needs to see on the paper whether the shop handed
    /// back money it already held or lent it (QARZ-08), because only the second leaves a debt.
    public decimal AdvanceBaseAmount { get; set; }
    public decimal LoanBaseAmount { get; set; }

    /// The customer's net position right after this document, in base currency: positive means
    /// they owe, negative means the shop does. Snapshotted because a document must keep saying
    /// what was true when it was issued, however long afterwards it is reprinted (HUJJ-03, HUJJ-05).
    public decimal BalanceAfterBase { get; set; }

    public string? Note { get; set; }
    public string? IdempotencyKey { get; set; }

    public ICollection<CustomerRefundTender> Tenders { get; set; } = [];
    public ICollection<Transaction> Transactions { get; set; } = [];
}

public class CustomerRefundTender : BaseEntity
{
    public long CustomerRefundDocumentId { get; set; }
    public CustomerRefundDocument Document { get; set; } = null!;
    public PaymentMethod Method { get; set; }
    public string Currency { get; set; } = "UZS";
    public decimal Amount { get; set; }
    public decimal Rate { get; set; } = 1m;
    public decimal AmountBase { get; set; }
}

public class CustomerReturnDocument : AuditableEntity, IBranchScoped
{
    public long BranchId { get; set; }
    public long WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;
    public long? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public long UserId { get; set; }
    public User User { get; set; } = null!;
    public string DocumentNumber { get; set; } = null!;
    public DateOnly BusinessDate { get; set; }
    public BusinessDocumentStatus Status { get; set; } = BusinessDocumentStatus.Posted;
    public decimal GrossAmount { get; set; }
    public decimal RefundAmount { get; set; }
    public decimal CashbackReversed { get; set; }
    public string? Note { get; set; }
    public string? IdempotencyKey { get; set; }

    public ICollection<CustomerReturnLine> Lines { get; set; } = [];
    public ICollection<CustomerReturnSettlement> Settlements { get; set; } = [];
    public ICollection<Transaction> Transactions { get; set; } = [];
}

public class CustomerReturnLine : BaseEntity
{
    public long CustomerReturnDocumentId { get; set; }
    public CustomerReturnDocument Document { get; set; } = null!;
    public long? SaleId { get; set; }
    public Sale? Sale { get; set; }
    public long? SaleItemId { get; set; }
    public SaleItem? SaleItem { get; set; }
    public long VariantId { get; set; }
    public ProductVariant Variant { get; set; } = null!;
    public long? StockId { get; set; }
    public Stock? Stock { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string PriceCurrency { get; set; } = "UZS";
    public decimal PriceRate { get; set; } = 1m;
    public decimal LineAmount { get; set; }
    public decimal CashbackReversed { get; set; }
    public string? Reason { get; set; }
    public ReturnItemCondition Condition { get; set; }
    public InventoryDisposition Disposition { get; set; }
}

public class CustomerReturnSettlement : BaseEntity
{
    public long CustomerReturnDocumentId { get; set; }
    public CustomerReturnDocument Document { get; set; } = null!;
    public ReturnSettlementMethod Method { get; set; }
    public string Currency { get; set; } = "UZS";
    public decimal Amount { get; set; }
    public decimal Rate { get; set; } = 1m;
    public decimal AmountBase { get; set; }
}

public class InventoryPosition : AuditableEntity, IBranchScoped
{
    public long BranchId { get; set; }
    public InventoryLocationKind LocationKind { get; set; }
    public long LocationId { get; set; }
    public long VariantId { get; set; }
    public ProductVariant Variant { get; set; } = null!;
    public decimal Quantity { get; set; }
}

public class InventoryMovement : AuditableEntity, IBranchScoped
{
    public long BranchId { get; set; }
    public long VariantId { get; set; }
    public ProductVariant Variant { get; set; } = null!;
    public decimal Quantity { get; set; }
    public InventoryMovementKind Kind { get; set; }
    public InventoryLocationKind FromLocationKind { get; set; }
    public long FromLocationId { get; set; }
    public InventoryLocationKind ToLocationKind { get; set; }
    public long ToLocationId { get; set; }
    public string SourceType { get; set; } = null!;
    public long SourceId { get; set; }
    public long UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
