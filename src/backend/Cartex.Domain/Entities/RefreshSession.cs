using Cartex.Domain.Common;

namespace Cartex.Domain.Entities;

public class RefreshSession : BaseEntity
{
    public long UserId { get; set; }
    public User User { get; set; } = null!;

    public string TokenHash { get; set; } = null!;
    public string? DeviceId { get; set; }
    public string? DeviceName { get; set; }
    public string? Client { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime FamilyCreatedAt { get; set; }
    public DateTime LastUsedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByHash { get; set; }
}
