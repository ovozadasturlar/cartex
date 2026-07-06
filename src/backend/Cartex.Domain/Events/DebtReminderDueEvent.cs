using Cartex.Domain.Common;

namespace Cartex.Domain.Events;

public sealed record DebtReminderDueEvent(long CustomerId, string CustomerName, decimal Balance, string Currency, int DaysOverdue, string Template = "debt_reminder", DateOnly? DueDate = null) : IDomainEvent;
