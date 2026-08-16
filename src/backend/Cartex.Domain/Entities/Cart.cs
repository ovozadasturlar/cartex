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
    public int Version { get; set; } = 1;

    public long? ClaimedByUserId { get; set; }
    public User? ClaimedByUser { get; set; }
    public DateTime? ClaimedAt { get; set; }
    public long? CancelledByUserId { get; set; }
    public User? CancelledByUser { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    public long? SaleId { get; set; }
    public Sale? Sale { get; set; }
    public long? RequeuedFromCartId { get; set; }
    public Cart? RequeuedFromCart { get; set; }

    /// Entered by whoever filled the cart. Carried into the sale untouched, so a discount
    /// agreed with the customer on the shop floor is not lost at the till.
    public decimal DiscountAmount { get; set; }

    public decimal PaidCash { get; set; }
    public decimal PaidCard { get; set; }
    public decimal PaidBonus { get; set; }
    public string? DebtCurrency { get; set; }
    public DateOnly? DebtDueDate { get; set; }
    public decimal CreditAmount { get; set; }
    public bool UseCustomerAdvance { get; set; } = true;

    public ICollection<CartItem> Items { get; set; } = [];
    public ICollection<CartParticipant> Participants { get; set; } = [];
    public ICollection<CartPayment> Payments { get; set; } = [];
}

public sealed class CartPayment : BaseEntity
{
    public long CartId { get; set; }
    public Cart Cart { get; set; } = null!;
    public PaymentMethod Method { get; set; }
    public string Currency { get; set; } = "UZS";
    public decimal Amount { get; set; }
}
