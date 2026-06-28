using Cartex.Domain.Common;

namespace Cartex.Domain.Entities;

public class Unit : SoftDeleteEntity
{
    public string Name { get; set; } = null!;
    public string ShortName { get; set; } = null!;

    public ICollection<Product> Products { get; set; } = [];
}
