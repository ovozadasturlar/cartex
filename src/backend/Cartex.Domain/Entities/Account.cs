using Cartex.Domain.Common;
using Cartex.Domain.Enums;

namespace Cartex.Domain.Entities;

public class Account : SoftDeleteEntity
{
    public long? BranchId { get; set; }
    public Branch? Branch { get; set; }

    public long? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public long? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public string Name { get; set; } = null!;
    public AccountType Type { get; set; }
    public string Currency { get; set; } = "UZS";
    public decimal Balance { get; set; }
}
