using Cartex.Domain.Common;
using Cartex.Domain.Enums;

namespace Cartex.Domain.Entities;

public class Unit : SoftDeleteEntity
{
    public string Name { get; set; } = null!;
    public string ShortName { get; set; } = null!;
    public UnitDimension Dimension { get; set; } = UnitDimension.Count;
    public decimal Factor { get; set; } = 1;
    public bool IsSystem { get; set; }

    public ICollection<Product> Products { get; set; } = [];
}
