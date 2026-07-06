using Cartex.Domain.Common;

namespace Cartex.Domain.Entities;

public class ShiftCash : BaseEntity
{
    public long ShiftId { get; set; }
    public Shift Shift { get; set; } = null!;

    public string Currency { get; set; } = null!;
    public decimal OpeningFloat { get; set; }
    public decimal? CountedCash { get; set; }
}
