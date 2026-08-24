using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Application.Sms;
using Cartex.Domain.Enums;
using Cartex.Domain.Common.Exceptions;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Infrastructure.Notifications.Sms;

public sealed class DeviceSmsProvider(SmsGatewayService gateway, IApplicationDbContext db) : ISmsProvider
{
    public string Name => "device";

    public async Task<SmsSendResult> SendAsync(
        SmsSettings settings,
        string password,
        string phone,
        string text,
        SmsSendContext context,
        CancellationToken cancellationToken)
    {
        if (context.BranchId is not long branchId)
            throw new BusinessRuleException("Qurilma SMS shlyuzi uchun filial aniqlanmadi.");
        if (!Enabled(settings, context.Kind))
            throw new BusinessRuleException("Bu SMS turi sozlamada o'chirilgan.");

        var store = await db.Businesses.AsNoTracking().Select(x => x.Name).FirstAsync(cancellationToken);
        var content = text.StartsWith(store, StringComparison.OrdinalIgnoreCase) ? text : $"{store}: {text}";
        var idempotencyKey = context.IdempotencyKey ?? Guid.NewGuid().ToString("N");
        var job = await gateway.CreateAsync(branchId, context.Kind, phone, content,
            SmsTextSegments.Count(content), idempotencyKey, context.CustomerId, cancellationToken,
            context.NotificationDeliveryId, context.NotificationDeliveryAttemptId);
        return new SmsSendResult(job?.Id.ToString(), true);
    }

    public async Task<NotificationDeliveryStatus?> GetStatusAsync(
        SmsSettings settings,
        string password,
        string providerMessageId,
        CancellationToken cancellationToken)
    {
        if (!long.TryParse(providerMessageId, out var id))
            return null;
        var status = await db.SmsGatewayJobs.AsNoTracking().Where(x => x.Id == id).Select(x => x.Status)
            .SingleOrDefaultAsync(cancellationToken);
        return status switch
        {
            SmsGatewayJobStatus.Delivered => NotificationDeliveryStatus.Delivered,
            SmsGatewayJobStatus.Failed or SmsGatewayJobStatus.Rejected or SmsGatewayJobStatus.Cancelled => NotificationDeliveryStatus.Undelivered,
            _ => (NotificationDeliveryStatus?)null
        };
    }

    private static bool Enabled(SmsSettings settings, SmsGatewayJobKind kind) => kind switch
    {
        SmsGatewayJobKind.DebtReminder => settings.DebtReminderEnabled,
        SmsGatewayJobKind.ReceiptLink => settings.ReceiptLinkEnabled,
        SmsGatewayJobKind.Promotion => settings.PromotionEnabled,
        SmsGatewayJobKind.Manual => settings.ManualEnabled,
        _ => throw new InvalidOperationException($"Noma'lum SMS turi: {kind}")
    };
}
