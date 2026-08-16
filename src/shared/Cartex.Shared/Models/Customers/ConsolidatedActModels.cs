namespace Cartex.Shared.Models.Customers;

/// One document the user picked. The kind is a string rather than an enum on the wire so a new
/// document type can be added without breaking older clients.
public sealed record ConsolidatedActSelection(string Kind, long Id);

public sealed record ConsolidatedActRequest(long CustomerId, List<ConsolidatedActSelection> Documents);

public sealed record ConsolidatedActDocumentDto(
    string Kind,
    long Id,
    string DocumentNumber,
    DateOnly BusinessDate,
    decimal Amount);

/// Net goods: what the customer kept once the selected returns are taken off.
public sealed record ConsolidatedActLineDto(
    long VariantId,
    string ProductName,
    string UnitName,
    decimal SoldQuantity,
    decimal ReturnedQuantity,
    decimal NetQuantity,
    decimal NetAmount);

public sealed record ConsolidatedActDto(
    long CustomerId,
    string CustomerName,
    DateOnly FromDate,
    DateOnly ToDate,
    IReadOnlyList<ConsolidatedActDocumentDto> Documents,
    IReadOnlyList<ConsolidatedActLineDto> Lines,
    decimal ConsumedAmount,
    decimal PaidAmount,
    decimal RemainingDebt);
