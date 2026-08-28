using Cartex.Shared.Models.Common;

namespace Cartex.Shared.Models.Suppliers;

public record SupplierDto(long Id, string Name, string? Phone, decimal Payable, bool AcceptsReturns = false)
{
    public IReadOnlyList<CurrencyAmountDto> PayableBalances { get; init; } = [];
}

public record SupplierTotalsDto(int Count, decimal TotalPayable, decimal TotalAdvance);

public record SupplierLedgerEntryDto(DateTime Date, string OperationType, string AccountType, decimal Change, decimal BalanceAfter, string? Currency = null);

public record PaySupplierDebtRequest(decimal Amount, string Method = "Cash", string? DebtCurrency = null, string? PayCurrency = null, long? SupplyId = null, string? IdempotencyKey = null);

public record SupplierPaymentDto(long TransactionId, DateTime CreatedAt, decimal Amount, string Currency, string Method, string? UserName);
