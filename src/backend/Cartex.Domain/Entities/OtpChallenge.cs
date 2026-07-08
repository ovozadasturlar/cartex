using Cartex.Domain.Common;

namespace Cartex.Domain.Entities;

public class OtpChallenge : AuditableEntity
{
    public long CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    public string CodeHash { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public int Attempts { get; set; }
    public DateTime? ConsumedAt { get; set; }
}
