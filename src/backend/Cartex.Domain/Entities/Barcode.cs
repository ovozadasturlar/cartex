using Cartex.Domain.Common;

namespace Cartex.Domain.Entities;

public class Barcode : SoftDeleteEntity
{
    public long VariantId { get; set; }
    public ProductVariant Variant { get; set; } = null!;

    public string Code { get; set; } = null!;
    public decimal PackQty { get; set; } = 1;
}
