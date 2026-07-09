using Cartex.Domain.Common;

namespace Cartex.Domain.Entities;

public class HardwareKey : AuditableEntity
{
    public long UserId { get; set; }
    public User User { get; set; } = null!;
    public string Serial { get; set; } = null!;
    public bool IsEnabled { get; set; } = true;
    public DateTime? RevokedAt { get; set; }
}
