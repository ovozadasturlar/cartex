namespace Cartex.Shared.Models.Reports;

public record DebtAgingRowDto(long CustomerId, string CustomerName, decimal Balance, string Currency, decimal BalanceBase, DateTime? LastActivity, int DaysOverdue, string Bucket);

public record DebtAgingReportDto(decimal Total, decimal Bucket0_30, decimal Bucket31_60, decimal Bucket60Plus, List<DebtAgingRowDto> Rows);
