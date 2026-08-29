using Cartex.Domain.Common;

namespace Cartex.Domain.Events;

public sealed record SaleCompletedEvent(string ReceiptToken, long? CustomerId, decimal TotalAmount) : IDomainEvent;
