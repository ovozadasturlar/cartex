using Cartex.Domain.Common;

namespace Cartex.Domain.Entities;

public class Barcode : SoftDeleteEntity
{
    public long ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public string Code { get; set; } = null!;
    public decimal PackQty { get; set; } = 1;
}
