namespace Cartex.Shared.Models.Customers;

public record CustomerTotalsDto(int Count, decimal TotalDebt, decimal TotalBonus, decimal TotalCredit = 0);
