namespace Cartex.Shared.Models.Customers;

public sealed record CustomerRefundTenderRequest(string Method, string Currency, decimal Amount);

public sealed record CreateCustomerRefundRequest(
    long CustomerId,
    long? BranchId,
    List<CustomerRefundTenderRequest> Tenders,
    DateOnly? BusinessDate = null,
    string? Note = null,
    string? IdempotencyKey = null);

public sealed record CustomerRefundCreatedDto(
    long Id,
    string DocumentNumber,
    decimal TotalBaseAmount,
    decimal AdvanceBaseAmount = 0,
    decimal LoanBaseAmount = 0);

public sealed record CustomerRefundTenderDto(
    string Method,
    string Currency,
    decimal Amount,
    decimal Rate,
    decimal AmountBase);

public sealed record CustomerRefundDocumentDto(
    long Id,
    string DocumentNumber,
    long BranchId,
    long CustomerId,
    string CustomerName,
    long UserId,
    string UserName,
    DateOnly BusinessDate,
    DateTime CreatedAt,
    string Status,
    decimal TotalBaseAmount,
    decimal AdvanceBaseAmount,
    decimal LoanBaseAmount,
    string? Note,
    IReadOnlyList<CustomerRefundTenderDto> Tenders);

public sealed record CustomerRefundListDto(
    long Id,
    string DocumentNumber,
    long CustomerId,
    string CustomerName,
    DateOnly BusinessDate,
    DateTime CreatedAt,
    string Status,
    decimal TotalBaseAmount,
    string? Note);
