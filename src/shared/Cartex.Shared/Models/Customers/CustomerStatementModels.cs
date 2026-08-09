namespace Cartex.Shared.Models.Customers;

public sealed record CustomerStatementBalanceDto(
    string Currency,
    decimal OpeningBalance,
    decimal ClosingBalance);

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
    long? SaleId = null,
    long? TradeCaseId = null);

public sealed record CustomerStatementProductDto(
    long VariantId,
    string ProductName,
    string UnitName,
    decimal Sold,
    decimal SaleReturned,
    decimal NetSold,
    decimal CustodyIssued,
    decimal CustodyReturned,
    decimal CustodySettled,
    decimal CustodyOutstanding,
    decimal ChargedBaseAmount);

public sealed record CustomerStatementDto(
    long CustomerId,
    string CustomerName,
    string? CustomerPhone,
    DateTime? From,
    DateTime? To,
    long? TradeCaseId,
    string? TradeCaseNumber,
    long? BranchId,
    string BaseCurrency,
    IReadOnlyList<CustomerStatementBalanceDto> Balances,
    IReadOnlyList<CustomerStatementEntryDto> Timeline,
    IReadOnlyList<CustomerStatementProductDto> Products,
    DateTime GeneratedAt);
