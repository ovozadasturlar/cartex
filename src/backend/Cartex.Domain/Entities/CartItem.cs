using Cartex.Domain.Common;

namespace Cartex.Domain.Entities;

public class CartItem : BaseEntity
{
    public long CartId { get; set; }
    public Cart Cart { get; set; } = null!;

    public long ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public decimal Quantity { get; set; }
}
