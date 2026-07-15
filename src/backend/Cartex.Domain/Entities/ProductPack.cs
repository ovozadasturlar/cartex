using Cartex.Domain.Common;
using Cartex.Domain.Enums;

namespace Cartex.Domain.Entities;

public class ProductPack : SoftDeleteEntity
{
    public long ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public string Name { get; set; } = null!;
    public decimal Size { get; set; }
    public PackKind Kind { get; set; } = PackKind.Purchase;
    public bool IsDefault { get; set; }
}
