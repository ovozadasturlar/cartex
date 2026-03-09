namespace Cartex.Shared.Models.Accounts;

public record AccountDto(long Id, string OwnerType, long OwnerId, string Name, string Type, decimal Balance);
