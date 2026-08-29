namespace Cartex.Application.Common.Interfaces;

public sealed record SmsSendContext(
    long? BranchId = null,
    Cartex.Domain.Enums.SmsGatewayJobKind Kind = Cartex.Domain.Enums.SmsGatewayJobKind.Manual,
    long? CustomerId = null,
    string? IdempotencyKey = null,
    long? NotificationDeliveryId = null,
    long? NotificationDeliveryAttemptId = null);

public interface ISmsService
{
    Task<NotificationProviderResult?> SendAsync(string phone, string text, CancellationToken cancellationToken = default);
    Task<NotificationProviderResult?> SendAsync(string phone, string text, SmsSendContext context, CancellationToken cancellationToken = default);
}
