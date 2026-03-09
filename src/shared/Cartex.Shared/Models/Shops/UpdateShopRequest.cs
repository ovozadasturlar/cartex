namespace Cartex.Shared.Models.Shops;

public record UpdateShopRequest(string Name, string? Address, string? Phone, decimal CashbackRate, bool IsActive);
