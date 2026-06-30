using Cartex.Domain.Common;
using Cartex.Domain.Enums;

namespace Cartex.Domain.Entities;

public class LoyaltyProgram : AuditableEntity
{
    public long? BranchId { get; set; }
    public Branch? Branch { get; set; }

    public bool IsEnabled { get; set; }
    public decimal TotalPercent { get; set; }

    public ICollection<CashbackRule> Rules { get; set; } = [];
}
