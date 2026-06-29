using Cartex.Domain.Common;

namespace Cartex.Domain.Entities;

public class BusinessSetting : AuditableEntity
{
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
}
