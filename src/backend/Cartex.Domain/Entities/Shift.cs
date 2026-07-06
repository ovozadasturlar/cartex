using Cartex.Domain.Common;
using Cartex.Domain.Enums;

namespace Cartex.Domain.Entities;

public class Shift : SoftDeleteEntity, IBranchScoped
{
    public long BranchId { get; set; }

    public long UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    public decimal OpeningFloat { get; set; }
    public decimal? CountedCash { get; set; }

    public ShiftStatus Status { get; set; } = ShiftStatus.Open;

    public ICollection<Transaction> Transactions { get; set; } = [];
    public ICollection<ShiftCash> CashRows { get; set; } = [];
}
