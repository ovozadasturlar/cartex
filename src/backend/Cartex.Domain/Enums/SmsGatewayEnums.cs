namespace Cartex.Domain.Enums;

public enum SmsGatewayJobKind
{
    DebtReminder,
    ReceiptLink,
    Promotion,
    Manual
}

public enum SmsGatewayJobStatus
{
    Pending,
    Assigned,
    Sent,
    Delivered,
    Simulated,
    Failed,
    Rejected,
    Cancelled
}
