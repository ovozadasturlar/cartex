namespace Cartex.Shared.Models.Shops;

public record CreateShopRequest(string Name, string? Address, string? Phone, decimal CashbackRate);
