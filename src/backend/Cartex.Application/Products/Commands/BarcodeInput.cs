namespace Cartex.Application.Products.Commands;

public record BarcodeInput(string Code, decimal PackQty = 1);
