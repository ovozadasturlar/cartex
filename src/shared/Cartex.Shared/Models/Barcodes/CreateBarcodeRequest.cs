namespace Cartex.Shared.Models.Barcodes;

public record CreateBarcodeRequest(long ProductId, string Code, decimal PackQty);
