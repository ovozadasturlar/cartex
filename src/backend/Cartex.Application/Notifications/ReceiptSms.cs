using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Settings;
using Cartex.Application.Sms;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Enums;
using Cartex.Domain.Events;
using Cartex.Persistence;
using Cartex.Shared.Models.Sales;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace Cartex.Application.Notifications;

public sealed record SendReceiptSmsCommand(long SaleId, string ConfirmationToken) : ICommand<ReceiptSmsResultDto>;
public sealed record GetReceiptSmsPreviewQuery(long SaleId) : IRequest<ReceiptSmsPreviewDto>;

public sealed class ReceiptSmsService(
    IApplicationDbContext db,
    ISettingsService settings,
    SmsGatewayService gateway)
{
    public async Task EnqueueAutomaticAsync(string receiptToken, CancellationToken cancellationToken)
    {
        var sms = await settings.GetAsync<SmsSettings>(SettingKeys.Sms, cancellationToken);
        if (sms is null || !sms.Enabled || !sms.SendReceiptOnSale)
            return;

        var target = await FindByTokenAsync(receiptToken, cancellationToken);
        if (target is null || string.IsNullOrWhiteSpace(target.Phone))
            return;

        await EnqueueAsync(target, sms, $"receipt-sms:{target.SaleId}", cancellationToken);
    }

    public async Task<ReceiptSmsResultDto> EnqueueManualAsync(
        long saleId,
        string confirmationToken,
        CancellationToken cancellationToken)
    {
        var payload = await GetManualPayloadAsync(saleId, cancellationToken);
        if (!string.Equals(confirmationToken, CreateConfirmationToken(payload), StringComparison.Ordinal))
            throw new BusinessRuleException("SMS tasdiqlangan mazmuni o'zgargan.", "receipt_sms_preview_changed");

        var job = await gateway.CreateAsync(
            payload.Target.BranchId,
            SmsGatewayJobKind.ReceiptLink,
            payload.Target.Phone!,
            payload.Text,
            SmsTextSegments.Count(payload.Text),
            $"receipt-sms:{saleId}:manual:{Guid.NewGuid():N}",
            payload.Target.CustomerId,
            cancellationToken) ?? throw new InvalidOperationException("Chek SMS ishi yaratilmadi.");
        return new ReceiptSmsResultDto(job.Id, job.Status.ToString());
    }

    public async Task<ReceiptSmsPreviewDto> PreviewManualAsync(long saleId, CancellationToken cancellationToken)
    {
        var payload = await GetManualPayloadAsync(saleId, cancellationToken);
        return new ReceiptSmsPreviewDto(payload.Target.Phone!, payload.Text, CreateConfirmationToken(payload));
    }

    private async Task<Cartex.Domain.Entities.SmsGatewayJob> EnqueueAsync(
        ReceiptSmsTarget target,
        SmsSettings sms,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var text = await ComposeTextAsync(target, sms, cancellationToken);

        return await gateway.CreateAsync(
            target.BranchId,
            SmsGatewayJobKind.ReceiptLink,
            target.Phone!,
            text.Trim(),
            SmsTextSegments.Count(text),
            idempotencyKey,
            target.CustomerId,
            cancellationToken) ?? throw new InvalidOperationException("Chek SMS ishi yaratilmadi.");
    }

    private async Task<ManualPayload> GetManualPayloadAsync(long saleId, CancellationToken cancellationToken)
    {
        var sms = await settings.GetAsync<SmsSettings>(SettingKeys.Sms, cancellationToken);
        if (sms is null || !sms.Enabled)
            throw new BusinessRuleException("SMS tizimi o'chiq.", "sms_disabled");

        var target = await FindByIdAsync(saleId, cancellationToken)
            ?? throw new NotFoundException("Savdo topilmadi.", "sale_not_found");
        if (string.IsNullOrWhiteSpace(target.Phone))
            throw new BusinessRuleException("Mijoz telefon raqami yo'q.", "customer_phone_missing");

        return new ManualPayload(target, await ComposeTextAsync(target, sms, cancellationToken));
    }

    private async Task<string> ComposeTextAsync(
        ReceiptSmsTarget target,
        SmsSettings sms,
        CancellationToken cancellationToken)
    {
        var notification = await settings.GetAsync<NotificationSettings>(SettingKeys.Notification, cancellationToken);
        if (string.IsNullOrWhiteSpace(notification?.PublicBaseUrl))
            throw new BusinessRuleException("Ommaviy chek manzili sozlanmagan.", "receipt_public_url_missing");

        var link = $"{notification.PublicBaseUrl.TrimEnd('/')}/r/{target.ReceiptToken}";
        // Mijoz havolani ochmasdan ham asosiy raqamlarni ko'rishi kerak: chek raqami,
        // summa, to'lov va qarz. Qarz yo'q bo'lsa qarzga oid o'rinlar bo'sh qoladi.
        var text = sms.ReceiptLinkTemplate
            .Replace("{store}", target.StoreName, StringComparison.Ordinal)
            .Replace("{link}", link, StringComparison.Ordinal)
            .Replace("{receipt}", target.SaleId.ToString(), StringComparison.Ordinal)
            .Replace("{total}", target.TotalAmount.ToString("N0"), StringComparison.Ordinal)
            .Replace("{paid}", target.PaidAmount.ToString("N0"), StringComparison.Ordinal)
            .Replace("{debt}", target.DebtAmount > 0 ? target.DebtAmount.ToString("N0") : string.Empty, StringComparison.Ordinal)
            .Replace("{date}", target.SaleDate.ToString("dd.MM.yyyy"), StringComparison.Ordinal)
            .Replace("{due}", target.DebtDueDate is { } due ? due.ToString("dd.MM.yyyy") : string.Empty, StringComparison.Ordinal);
        if (!text.StartsWith(target.StoreName, StringComparison.OrdinalIgnoreCase))
            text = $"{target.StoreName}: {text}";
        if (!text.Contains(link, StringComparison.Ordinal))
            text = $"{text} {link}";
        return text.Trim();
    }

    private Task<ReceiptSmsTarget?> FindByTokenAsync(string token, CancellationToken cancellationToken) =>
        db.Sales.Where(x => x.ReceiptToken == token)
            .Select(x => new ReceiptSmsTarget(
                x.Id,
                x.BranchId,
                x.CustomerId,
                x.Customer == null ? null : x.Customer.Phone,
                x.ReceiptToken,
                x.Warehouse.Branch.Business.Name,
                x.TotalAmount,
                x.TotalAmount - x.DebtAmount,
                x.DebtAmount,
                x.CreatedAt,
                x.DebtDueDate))
            .SingleOrDefaultAsync(cancellationToken);

    private Task<ReceiptSmsTarget?> FindByIdAsync(long saleId, CancellationToken cancellationToken) =>
        db.Sales.Where(x => x.Id == saleId)
            .Select(x => new ReceiptSmsTarget(
                x.Id,
                x.BranchId,
                x.CustomerId,
                x.Customer == null ? null : x.Customer.Phone,
                x.ReceiptToken,
                x.Warehouse.Branch.Business.Name,
                x.TotalAmount,
                x.TotalAmount - x.DebtAmount,
                x.DebtAmount,
                x.CreatedAt,
                x.DebtDueDate))
            .SingleOrDefaultAsync(cancellationToken);

    private sealed record ReceiptSmsTarget(
        long SaleId,
        long BranchId,
        long? CustomerId,
        string? Phone,
        string ReceiptToken,
        string StoreName,
        decimal TotalAmount,
        decimal PaidAmount,
        decimal DebtAmount,
        DateTime SaleDate,
        DateOnly? DebtDueDate);

    private sealed record ManualPayload(ReceiptSmsTarget Target, string Text);

    private static string CreateConfirmationToken(ManualPayload payload) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{payload.Target.Phone}\n{payload.Text}")));
}

public sealed class ReceiptSmsSaleCompletedHandler(ReceiptSmsService service)
    : INotificationHandler<DomainEventNotification<SaleCompletedEvent>>
{
    public Task Handle(DomainEventNotification<SaleCompletedEvent> notification, CancellationToken cancellationToken) =>
        service.EnqueueAutomaticAsync(notification.DomainEvent.ReceiptToken, cancellationToken);
}

public sealed class SendReceiptSmsCommandHandler(
    ReceiptSmsService service,
    ICurrentUser currentUser,
    IAuditService audit)
    : IRequestHandler<SendReceiptSmsCommand, ReceiptSmsResultDto>
{
    public async Task<ReceiptSmsResultDto> Handle(SendReceiptSmsCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(AppPermissions.Customers.Message))
            throw new ForbiddenException("Mijozga xabar yuborish ruxsati kerak.");

        var result = await service.EnqueueManualAsync(request.SaleId, request.ConfirmationToken, cancellationToken);
        audit.SetOutcome("receipt.sms.manual", "sales", request.SaleId,
            new { result.JobId, result.Status }, "Chek SMS navbatiga qo'yildi");
        return result;
    }
}

public sealed class GetReceiptSmsPreviewQueryHandler(
    ReceiptSmsService service,
    ICurrentUser currentUser)
    : IRequestHandler<GetReceiptSmsPreviewQuery, ReceiptSmsPreviewDto>
{
    public Task<ReceiptSmsPreviewDto> Handle(GetReceiptSmsPreviewQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(AppPermissions.Customers.Message))
            throw new ForbiddenException("Mijozga xabar yuborish ruxsati kerak.");

        return service.PreviewManualAsync(request.SaleId, cancellationToken);
    }
}
