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
    public decimal DiscountAmount { get; set; }

    /// How much of <see cref="DiscountAmount"/> came from rounding the payable down.
    /// Reporting only: the total is already net of it.
    public decimal RoundingAmount { get; set; }
    public decimal PaidCash { get; set; }
    public decimal PaidCard { get; set; }
    public decimal PaidBonus { get; set; }
    public decimal PaidAdvance { get; set; }
    public decimal DebtAmount { get; set; }
    public DateOnly? DebtDueDate { get; set; }
    public string DebtCurrency { get; set; } = "UZS";
    public decimal DebtRate { get; set; } = 1m;
    public decimal ChangeAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public decimal CashbackEarned { get; set; }
    public decimal RefundedCash { get; set; }
    public decimal RefundedCard { get; set; }
    public decimal RefundedBonus { get; set; }
    public decimal RefundedDebt { get; set; }
    public decimal RefundedAdvance { get; set; }
    public decimal ReturnNoChargeAmount { get; set; }
    public SaleStatus Status { get; set; } = SaleStatus.Completed;
    public long? ShiftId { get; set; }
    public Shift? Shift { get; set; }
    public DateTime? VoidedAt { get; set; }
    public string? VoidReason { get; set; }
    /// HUJJ-02: the human-readable number of the sale document. The receipt token is a random
    /// public link; this is what a customer and an accountant quote at each other.
    public string DocumentNumber { get; set; } = null!;
    public string ReceiptToken { get; set; } = null!;
    public string? Note { get; set; }
    public string? IdempotencyKey { get; set; }

    public ICollection<SaleItem> Items { get; set; } = [];
    public ICollection<SalePayment> Payments { get; set; } = [];
    public ICollection<SaleParticipant> Participants { get; set; } = [];
}
