using Cartex.Domain.Common;

namespace Cartex.Domain.Entities;

public class ProductPrice : SoftDeleteEntity
{
    public long ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public long? WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    public decimal SellingPrice { get; set; }
}
