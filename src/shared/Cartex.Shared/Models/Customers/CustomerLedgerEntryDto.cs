namespace Cartex.Shared.Models.Customers;

public record CustomerLedgerEntryDto(
    DateTime Date,
    string OperationType,
    string AccountType,
    decimal Change,
    decimal BalanceAfter,
    string? Currency = null,
    long TransactionId = 0,
    long? PaymentDocumentId = null,
    string? PaymentNumber = null,
    long? SaleId = null);
