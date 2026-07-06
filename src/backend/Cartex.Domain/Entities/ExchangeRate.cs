using Cartex.Domain.Common;

namespace Cartex.Domain.Entities;

public class ExchangeRate : BaseEntity
{
    public string Code { get; set; } = null!;
    public decimal Rate { get; set; }
    public DateTime EffectiveAt { get; set; } = DateTime.UtcNow;
    public string Source { get; set; } = "manual";
    public long UserId { get; set; }
}
