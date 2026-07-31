namespace Cartex.Domain.Enums;

[Obsolete("Legacy SMS journal status. Use NotificationDeliveryStatus for new notifications.")]
public enum SmsStatus
{
    Sent,
    Failed,
    Delivered,
    Undelivered
}
