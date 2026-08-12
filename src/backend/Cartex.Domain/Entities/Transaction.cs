using Cartex.Domain.Common;
using Cartex.Domain.Enums;

namespace Cartex.Domain.Entities;

public class Transaction : AuditableEntity
{
    public long? FromAccountId { get; set; }
    public Account? FromAccount { get; set; }

    public long? ToAccountId { get; set; }
    public Account? ToAccount { get; set; }

    public decimal Amount { get; set; }
    public string Currency { get; set; } = "UZS";
    public decimal Rate { get; set; } = 1m;
    public OperationType OperationType { get; set; }
    public string? Description { get; set; }
    public string? IdempotencyKey { get; set; }

    public long? ExpenseCategoryId { get; set; }
    public ExpenseCategory? ExpenseCategory { get; set; }

    public long? SaleId { get; set; }
    public Sale? Sale { get; set; }

    public long? CustomerPaymentDocumentId { get; set; }
    public CustomerPaymentDocument? CustomerPaymentDocument { get; set; }

    public long? CustomerReturnDocumentId { get; set; }
    public CustomerReturnDocument? CustomerReturnDocument { get; set; }

    public long? CustomerRefundDocumentId { get; set; }
    public CustomerRefundDocument? CustomerRefundDocument { get; set; }

    public long? PartnerRedemptionDocumentId { get; set; }
    public PartnerRedemptionDocument? PartnerRedemptionDocument { get; set; }

    public long? SupplyId { get; set; }
    public Supply? Supply { get; set; }

    public long? ShiftId { get; set; }
    public Shift? Shift { get; set; }

    public long? BranchId { get; set; }

    public long UserId { get; set; }
    public User User { get; set; } = null!;
}
