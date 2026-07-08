using Cartex.Domain.Common;

namespace Cartex.Domain.Events;

public sealed record CartSubmittedEvent(string AggregateCode, long? CustomerId, int ItemCount, string WarehouseName) : IDomainEvent;
