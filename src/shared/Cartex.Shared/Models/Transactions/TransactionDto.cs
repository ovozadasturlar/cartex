namespace Cartex.Shared.Models.Transactions;

public record TransactionDto(
    long Id,
    decimal Amount,
    string OperationType,
    string? FromAccountName,
    string? ToAccountName,
    DateTime CreatedAt,
    string UserName);
