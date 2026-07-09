using Cartex.Domain.Common;

namespace Cartex.Domain.Entities;

public class Manufacturer : SoftDeleteEntity
{
    public string Name { get; set; } = null!;
}
