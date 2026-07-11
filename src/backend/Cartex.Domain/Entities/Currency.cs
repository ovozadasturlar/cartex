using Cartex.Domain.Common;

namespace Cartex.Domain.Entities;

public class Currency : BaseEntity
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = "";
    public bool IsSystem { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsDefault { get; set; }
}
