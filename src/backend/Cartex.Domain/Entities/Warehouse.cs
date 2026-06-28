using Cartex.Domain.Common;

namespace Cartex.Domain.Entities;

public class Warehouse : SoftDeleteEntity
{
    public long ShopId { get; set; }
    public Shop Shop { get; set; } = null!;

    public string Name { get; set; } = null!;

    public ICollection<Stock> Stocks { get; set; } = [];
}
