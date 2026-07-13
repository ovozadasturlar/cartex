using Cartex.Domain.Common;

namespace Cartex.Domain.Entities;

public class Barcode : SoftDeleteEntity
{
    public long VariantId { get; set; }
    public ProductVariant Variant { get; set; } = null!;

    public string Code { get; set; } = null!;

    // Bitta skanerlash necha saqlash birligini bildiradi (1 kg paket -> 1, quti -> 12).
    public decimal PackQty { get; set; } = 1;

    // Sotuv qadog'i presetiga bog'langan bo'lsa, PackQty o'sha qadoq hajmiga teng bo'lib turadi.
    public long? PackId { get; set; }
    public ProductPack? Pack { get; set; }
}
