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

    public long? TradeCaseId { get; set; }
    public TradeCase? TradeCase { get; set; }

    public decimal TotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
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
    public decimal RefundedCashback { get; set; }
    public SaleStatus Status { get; set; } = SaleStatus.Completed;
    public string ReceiptToken { get; set; } = null!;
    public string? IdempotencyKey { get; set; }

    public ICollection<SaleItem> Items { get; set; } = [];
    public ICollection<SalePayment> Payments { get; set; } = [];
    public ICollection<SaleParticipant> Participants { get; set; } = [];
}
