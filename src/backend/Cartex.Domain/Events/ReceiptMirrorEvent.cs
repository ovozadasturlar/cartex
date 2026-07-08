using Cartex.Domain.Common;

namespace Cartex.Domain.Events;

public sealed record ReceiptMirrorEvent(string ReceiptToken) : IDomainEvent;
