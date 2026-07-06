namespace Cartex.Shared.Models.Accounts;

public record AccountDto(long Id, string Name, string Type, string Currency, decimal Balance, string? OwnerName);
