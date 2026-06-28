namespace Cartex.Shared.Models.Products;

public record ProductLookupDto(long ProductId, string ProductName, string UnitName, decimal PackQty, decimal SellingPrice, decimal OnHand);
