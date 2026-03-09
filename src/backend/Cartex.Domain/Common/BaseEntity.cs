namespace Cartex.Domain.Common;

public abstract class BaseEntity
{
    public long Id { get; set; }
}

public abstract class AuditableEntity : BaseEntity
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
