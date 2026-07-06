using Cartex.Domain.Common;

namespace Cartex.Application.Common.Messaging;

public sealed record DomainEventNotification<TEvent>(TEvent DomainEvent) : INotification where TEvent : IDomainEvent;
