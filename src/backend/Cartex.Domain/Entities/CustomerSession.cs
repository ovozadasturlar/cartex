using Cartex.Domain.Common;

namespace Cartex.Domain.Entities;

public class CustomerSession : BaseEntity
{
    public long CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    public string TokenHash { get; set; } = null!;
    public string? DeviceName { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime FamilyCreatedAt { get; set; }
    public DateTime LastUsedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByHash { get; set; }
}
