using Cartex.Domain.Common;

namespace Cartex.Domain.Entities;

public class Category : BaseEntity
{
    public string Name { get; set; } = null!;
    public long? ParentId { get; set; }
    public Category? Parent { get; set; }

    public ICollection<Category> Children { get; set; } = [];
    public ICollection<Product> Products { get; set; } = [];
}
