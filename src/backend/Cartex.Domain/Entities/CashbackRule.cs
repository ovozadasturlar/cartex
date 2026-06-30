using Cartex.Domain.Common;
using Cartex.Domain.Enums;

namespace Cartex.Domain.Entities;

public class CashbackRule : AuditableEntity
{
    public long LoyaltyProgramId { get; set; }
    public LoyaltyProgram LoyaltyProgram { get; set; } = null!;

    public CashbackScope Scope { get; set; }
    public long TargetId { get; set; }
    public CashbackMethod Method { get; set; }
    public decimal Value { get; set; }
    public int Priority { get; set; }
    public bool ExcludeFromTotalPercent { get; set; }
}
