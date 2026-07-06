using Cartex.Domain.Common;
using Cartex.Domain.Enums;

namespace Cartex.Domain.Entities;

public class SalePayment : BaseEntity
{
    public long SaleId { get; set; }
    public Sale Sale { get; set; } = null!;

    public PaymentMethod Method { get; set; }
    public string Currency { get; set; } = "UZS";
    public decimal Amount { get; set; }
    public decimal Rate { get; set; } = 1m;
    public decimal AmountBase { get; set; }
}
