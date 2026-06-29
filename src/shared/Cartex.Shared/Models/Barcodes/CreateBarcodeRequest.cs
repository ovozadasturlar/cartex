namespace Cartex.Shared.Models.Barcodes;

public record CreateBarcodeRequest(long VariantId, string Code, decimal PackQty);
