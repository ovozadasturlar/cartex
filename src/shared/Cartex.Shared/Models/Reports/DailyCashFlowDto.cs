namespace Cartex.Shared.Models.Reports;

public record DailyCashFlowDto(DateTime Date, decimal Income, decimal Expense, decimal Sales);
