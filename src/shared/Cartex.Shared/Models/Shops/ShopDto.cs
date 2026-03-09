namespace Cartex.Shared.Models.Shops;

public record ShopDto(long Id, string Name, string? Address, string? Phone, decimal CashbackRate, bool IsActive);
