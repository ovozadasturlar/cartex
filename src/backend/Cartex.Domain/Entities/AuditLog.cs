using Cartex.Domain.Common;

namespace Cartex.Domain.Entities;

public class AuditLog : BaseEntity
{
    public long? UserId { get; set; }
    public User? User { get; set; }

    public string Action { get; set; } = null!;
    public string TableName { get; set; } = null!;
    public long? RecordId { get; set; }
    public string? OldData { get; set; }
    public string? NewData { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
