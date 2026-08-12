namespace Cartex.Shared.Models.Customers;

public sealed record CustomerStatementBalanceDto(
    string Currency,
    decimal OpeningBalance,
    decimal ClosingBalance);

public sealed record CustomerStatementSummaryDto(
    int SaleCount,
    decimal SaleAmount,
    int PaymentCount,
    decimal PaymentAmount,
    int ReturnCount,
    decimal ReturnAmount,
    int RefundCount,
    decimal RefundAmount);

public sealed record CustomerStatementEntryDto(
    DateTime OccurredAt,
    string Type,
    long? DocumentId,
    string DocumentNumber,
    string Summary,
    decimal Debit,
    decimal Credit,
    decimal RunningBalance,
    string Currency,
    long? SaleId = null);

public sealed record CustomerStatementProductDto(
    long VariantId,
    string ProductName,
    string UnitName,
    decimal Sold,
    decimal Returned,
    decimal NetSold,
    decimal ChargedBaseAmount);

public sealed record CustomerStatementDto(
    long CustomerId,
    string CustomerName,
    string? CustomerPhone,
    DateTime? From,
    DateTime? To,
    long? BranchId,
    string BaseCurrency,
    CustomerStatementSummaryDto Summary,
    IReadOnlyList<CustomerStatementBalanceDto> Balances,
    IReadOnlyList<CustomerStatementEntryDto> Timeline,
    IReadOnlyList<CustomerStatementProductDto> Products,
    DateTime GeneratedAt);
