using Cartex.Domain.Common;

namespace Cartex.Domain.Entities;

public class Customer : SoftDeleteEntity
{
    public string FullName { get; set; } = null!;
    public string? Phone { get; set; }
    public string? CardBarcode { get; set; }
    public decimal DiscountPct { get; set; }
    public decimal CashbackBalance { get; set; }

    public ICollection<Account> Accounts { get; set; } = [];
    public ICollection<Sale> Sales { get; set; } = [];
}
