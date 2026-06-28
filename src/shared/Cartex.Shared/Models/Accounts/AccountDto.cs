namespace Cartex.Shared.Models.Accounts;

public record AccountDto(long Id, string Name, string Type, decimal Balance, string? OwnerName);
