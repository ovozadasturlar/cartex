using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Settings;
using Cartex.Application.Sales.Queries;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;

namespace Cartex.Infrastructure.Notifications;

public sealed class NotificationService(
    ISettingsService settings,
    ITelegramService telegram,
    IEmailService email,
    ISmsService sms,
    ISender sender,
    IReceiptPdfRenderer pdfRenderer,
    IApplicationDbContext db) : INotificationService
{
    public async Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        if (message.Template == "sale_receipt")
        {
            await SendReceiptAsync(message, cancellationToken);
            return;
        }

        var text = await BuildTextAsync(message, cancellationToken);
        await SendTrackedAsync(message, Subject(message.Template), text, null, cancellationToken);
    }

    private async Task SendReceiptAsync(NotificationMessage message, CancellationToken cancellationToken)
    {
        if (!message.Data.TryGetValue("receiptToken", out var token))
            return;

        var cfg = await settings.GetAsync<NotificationSettings>(SettingKeys.Notification, cancellationToken) ?? new();
        var mode = ReceiptDeliveryPolicy.Resolve(message.Channel, cfg);
        var total = message.Data.GetValueOrDefault("total", "");
        var lang = message.Data.GetValueOrDefault("lang");
        string T(string key) => Cartex.Shared.Localization.ReceiptTexts.Get(key, lang);

        if (mode == "link")
        {
            var baseUrl = cfg.PublicBaseUrl!.TrimEnd('/');
            var text = $"{T("thanks")} {T("your_receipt")}: {baseUrl}/r/{token}" + (string.IsNullOrEmpty(total) ? "" : $" ({total})");
            await SendTrackedAsync(message, T("your_receipt"), text, null, cancellationToken);
            return;
        }

        if (mode == "text")
        {
            var fullReceipt = await sender.Send(new GetReceiptByTokenQuery(token), cancellationToken);
            var text = fullReceipt is null
                ? $"{T("your_purchase")}: {total}. {T("thanks")}"
                : ReceiptTextRenderer.Render(fullReceipt, await settings.GetAsync<ReceiptSettings>(SettingKeys.Receipt, cancellationToken));
            await SendTrackedAsync(message, T("your_receipt"), text, null, cancellationToken);
            return;
        }

        var receipt = await sender.Send(new GetReceiptByTokenQuery(token), cancellationToken);
        if (receipt is null)
            return;

        var receiptCfg = await settings.GetAsync<ReceiptSettings>(SettingKeys.Receipt, cancellationToken) ?? new();
        receiptCfg.PublicReceiptBaseUrl = cfg.PublicBaseUrl;
        var pdf = receiptCfg.PaperFormat switch
        {
            "A4" => pdfRenderer.RenderDocument(receipt, receiptCfg, a4: true),
            "A5" => pdfRenderer.RenderDocument(receipt, receiptCfg),
            _ => pdfRenderer.Render(receipt, receiptCfg)
        };
        var attachment = new EmailAttachment(pdf, $"chek-{token[..8]}.pdf");
        var caption = $"{T("thanks")} ({total})";
        await SendTrackedAsync(message, T("your_receipt"), caption, attachment, cancellationToken);
    }

    private async Task SendTrackedAsync(
        NotificationMessage message,
        string subject,
        string content,
        EmailAttachment? attachment,
        CancellationToken cancellationToken)
    {
        var provider = await ResolveProviderAsync(message.Channel, cancellationToken);
        var delivery = new NotificationDelivery
        {
            CustomerId = message.CustomerId,
            Channel = message.Channel,
            Purpose = message.Template,
            Recipient = message.Recipient,
            Subject = subject,
            Content = content
        };
        var attempt = new NotificationDeliveryAttempt
        {
            NotificationDelivery = delivery,
            AttemptNumber = 1,
            Provider = provider
        };
        delivery.Attempts.Add(attempt);
        db.NotificationDeliveries.Add(delivery);
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            var result = message.Channel switch
            {
                NotificationChannel.Telegram when attachment is not null =>
                    await telegram.SendDocumentAsync(message.Recipient, attachment.Content, attachment.FileName, content, cancellationToken),
                NotificationChannel.Telegram =>
                    await telegram.SendMessageAsync(message.Recipient, content, cancellationToken),
                NotificationChannel.Email =>
                    await email.SendAsync(message.Recipient, subject, content, cancellationToken, attachment),
                NotificationChannel.Sms =>
                    await sms.SendAsync(message.Recipient, content, cancellationToken),
                _ => null
            };

            var now = DateTime.UtcNow;
            if (result is null)
            {
                delivery.Status = NotificationDeliveryStatus.Skipped;
                delivery.CompletedAt = now;
                attempt.Status = NotificationDeliveryStatus.Skipped;
                attempt.CompletedAt = now;
                attempt.ErrorMessage = "Kanal sozlanmagan yoki adapter mavjud emas.";
            }
            else
            {
                delivery.Status = NotificationDeliveryStatus.Accepted;
                delivery.AcceptedAt = now;
                attempt.Provider = result.Provider;
                attempt.ProviderMessageId = result.ProviderMessageId;
                attempt.Units = Math.Max(1, result.Units);
                attempt.Status = NotificationDeliveryStatus.Accepted;
                attempt.AcceptedAt = now;
                attempt.CompletedAt = now;
            }
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            var now = DateTime.UtcNow;
            delivery.Status = NotificationDeliveryStatus.Failed;
            delivery.CompletedAt = now;
            attempt.Status = NotificationDeliveryStatus.Failed;
            attempt.CompletedAt = now;
            attempt.ErrorCode = ex.GetType().Name;
            attempt.ErrorMessage = Truncate(ex.Message, 1000);
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<string> ResolveProviderAsync(NotificationChannel channel, CancellationToken cancellationToken) =>
        channel switch
        {
            NotificationChannel.Sms =>
                (await settings.GetAsync<SmsSettings>(SettingKeys.Sms, cancellationToken))?.Provider ?? "sms:unconfigured",
            NotificationChannel.Email =>
                (await settings.GetAsync<EmailSettings>(SettingKeys.Email, cancellationToken))?.Host ?? "smtp:unconfigured",
            NotificationChannel.Telegram => "api.telegram.org",
            NotificationChannel.AppPush => "push:unconfigured",
            _ => "unknown"
        };

    private async Task<string> BuildTextAsync(NotificationMessage message, CancellationToken cancellationToken)
    {
        var name = message.Data.GetValueOrDefault("name", "");
        var balance = message.Data.GetValueOrDefault("balance", "");
        var currency = message.Data.GetValueOrDefault("currency", "");
        var days = message.Data.GetValueOrDefault("days", "");
        var dueDate = message.Data.GetValueOrDefault("dueDate", "");

        if (message.Template is "debt_reminder" or "debt_due_soon" or "debt_due_today")
        {
            var reminder = await settings.GetAsync<ReminderSettings>(SettingKeys.Reminder, cancellationToken);
            var template = message.Template switch
            {
                "debt_due_soon" => reminder?.DueSoonTemplate,
                "debt_due_today" => reminder?.DueTodayTemplate,
                _ => reminder?.OverdueTemplate
            };
            if (!string.IsNullOrWhiteSpace(template))
                return ApplyVariables(template, name, balance, currency, days, dueDate);
        }

        return message.Template switch
        {
            "debt_reminder" => $"Hurmatli {name}! Do'kondan qarzingiz: {balance} {currency} ({days} kundan beri). Iltimos, to'lovni amalga oshiring.",
            "debt_due_soon" => $"Hurmatli {name}! {balance} {currency} qarzingizni to'lash muddati {dueDate}.",
            "debt_due_today" => $"Hurmatli {name}! {balance} {currency} qarzingizni to'lash muddati bugun.",
            _ => message.Template
        };
    }

    private static string ApplyVariables(string template, string name, string balance, string currency, string days, string dueDate) =>
        template.Replace("{name}", name)
            .Replace("{balance}", balance)
            .Replace("{currency}", currency)
            .Replace("{days}", days)
            .Replace("{dueDate}", dueDate);

    private static string Subject(string template) =>
        template.StartsWith("debt_", StringComparison.Ordinal) ? "Qarz eslatmasi" : "Xabarnoma";

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
