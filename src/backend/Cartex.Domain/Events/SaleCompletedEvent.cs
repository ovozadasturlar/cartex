using Cartex.Domain.Common;

namespace Cartex.Domain.Events;

public sealed record SaleCompletedEvent(string ReceiptToken, long BranchId, long? CustomerId, decimal TotalAmount) : IDomainEvent;
