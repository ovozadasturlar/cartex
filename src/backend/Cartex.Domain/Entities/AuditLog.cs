using Cartex.Domain.Common;

namespace Cartex.Domain.Entities;

public class AuditLog : BaseEntity
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    public long? UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Stable machine-readable event code (legacy name kept for API compatibility).</summary>
    public string Action { get; set; } = null!;
    /// <summary>Primary subject type (legacy name kept for API compatibility).</summary>
    public string TableName { get; set; } = null!;
    public long? RecordId { get; set; }
    public string? Summary { get; set; }
    public string? CommandName { get; set; }
    public string? OldData { get; set; }
    public string? NewData { get; set; }
    public string? Details { get; set; }
    public int EntityCount { get; set; }
    public long? BranchId { get; set; }
    public string? Client { get; set; }
    public string? DeviceId { get; set; }
    public string? DeviceName { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? CorrelationId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
