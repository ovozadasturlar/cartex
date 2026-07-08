using Cartex.Domain.Common;

namespace Cartex.Domain.Entities;

public class ProductType : SoftDeleteEntity
{
    public string Name { get; set; } = null!;
    public bool TracksExpiry { get; set; }
    public string? AttributeSchema { get; set; }

    public ICollection<Product> Products { get; set; } = [];
}
