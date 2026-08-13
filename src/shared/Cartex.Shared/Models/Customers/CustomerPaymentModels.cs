namespace Cartex.Shared.Models.Customers;

public record CustomerPaymentTenderRequest(string Method, string Currency, decimal Amount);

public record CustomerPaymentAllocationRequest(string Currency, decimal Amount, long? SaleId = null);

public record CreateCustomerPaymentRequest(
    long CustomerId,
    long? BranchId,
    List<CustomerPaymentTenderRequest> Tenders,
    List<CustomerPaymentAllocationRequest>? Allocations = null,
    bool AutoAllocateDebt = true,
    DateOnly? BusinessDate = null,
    string? Note = null,
    string? IdempotencyKey = null);

public record CustomerPaymentCreatedDto(
    long Id,
    string DocumentNumber,
    decimal TotalBaseAmount,
    decimal AllocatedBaseAmount,
    decimal AdvanceBaseAmount);

public record CustomerPaymentTenderDto(
    string Method,
    string Currency,
    decimal Amount,
    decimal Rate,
    decimal AmountBase);

public record CustomerPaymentAllocationDto(
    long Id,
    long? SaleId,
    string Currency,
    decimal Amount,
    decimal Rate,
    decimal AmountBase);

public record CustomerPaymentDocumentDto(
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
    decimal AllocatedBaseAmount,
    decimal AdvanceBaseAmount,
    string? Note,
    IReadOnlyList<CustomerPaymentTenderDto> Tenders,
    IReadOnlyList<CustomerPaymentAllocationDto> Allocations);

public record CustomerPaymentListDto(
    long Id,
    string DocumentNumber,
    long CustomerId,
    string CustomerName,
    DateOnly BusinessDate,
    DateTime CreatedAt,
    string Status,
    decimal TotalBaseAmount,
    decimal AllocatedBaseAmount,
    decimal AdvanceBaseAmount,
    string? Note);

public record VoidCustomerPaymentRequest(string Reason);
