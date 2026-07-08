using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Settings;
using Cartex.Application.Sales.Queries;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Microsoft.Extensions.Logging;

namespace Cartex.Infrastructure.Notifications;

public sealed class NotificationService(
    ISettingsService settings,
    ITelegramService telegram,
    IEmailService email,
    ISmsService sms,
    ISender sender,
    IReceiptPdfRenderer pdfRenderer,
    IFeatureStateProvider features,
    ILogger<NotificationService> logger) : INotificationService
{
    public async Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        var feature = message.Channel switch
        {
            NotificationChannel.Telegram => FeatureCatalog.Telegram,
            NotificationChannel.Email => FeatureCatalog.Email,
            NotificationChannel.Sms => FeatureCatalog.Sms,
            _ => null
        };
        if (feature is not null && !await features.IsEnabledAsync(feature, cancellationToken))
        {
            logger.LogInformation("Channel {Channel} feature disabled; notification skipped", message.Channel);
            return;
        }

        if (message.Template == "sale_receipt")
        {
            await SendReceiptAsync(message, cancellationToken);
            return;
        }

        var text = await BuildTextAsync(message, cancellationToken);
        await SendTextAsync(message.Channel, message.Recipient, text, Subject(message.Template), cancellationToken);
    }

    private async Task SendReceiptAsync(NotificationMessage message, CancellationToken cancellationToken)
    {
        if (!message.Data.TryGetValue("receiptToken", out var token))
            return;

        var cfg = await settings.GetAsync<NotificationSettings>(SettingKeys.Notification, cancellationToken) ?? new();
        var mode = ReceiptDeliveryPolicy.Resolve(message.Channel, cfg);
        var total = message.Data.TryGetValue("total", out var t) ? t : "";
        var lang = message.Data.TryGetValue("lang", out var l) ? l : null;
        string T(string key) => Cartex.Shared.Localization.ReceiptTexts.Get(key, lang);

        if (mode == "link")
        {
            var baseUrl = cfg.PublicBaseUrl!.TrimEnd('/');
            var text = $"{T("thanks")} {T("your_receipt")}: {baseUrl}/r/{token}" + (string.IsNullOrEmpty(total) ? "" : $" ({total})");
            await SendTextAsync(message.Channel, message.Recipient, text, T("your_receipt"), cancellationToken);
            return;
        }

        if (mode == "text")
        {
            var text = $"{T("your_purchase")}: {total}. {T("thanks")}";
            await SendTextAsync(message.Channel, message.Recipient, text, T("your_receipt"), cancellationToken);
            return;
        }

        var receipt = await sender.Send(new GetReceiptByTokenQuery(token), cancellationToken);
        if (receipt is null)
            return;

        var receiptCfg = await settings.GetAsync<ReceiptSettings>(SettingKeys.Receipt, cancellationToken);
        var pdf = pdfRenderer.Render(receipt, receiptCfg);
        var fileName = $"chek-{token[..8]}.pdf";
        var caption = $"{T("thanks")} ({total})";

        switch (message.Channel)
        {
            case NotificationChannel.Telegram:
                await telegram.SendDocumentAsync(message.Recipient, pdf, fileName, caption, cancellationToken);
                break;
            case NotificationChannel.Email:
                await email.SendAsync(message.Recipient, T("your_receipt"), caption, cancellationToken, new EmailAttachment(pdf, fileName));
                break;
            default:
                logger.LogInformation("PDF receipt not supported for {Channel}; skipped", message.Channel);
                break;
        }
    }

    private async Task SendTextAsync(NotificationChannel channel, string recipient, string text, string subject, CancellationToken cancellationToken)
    {
        switch (channel)
        {
            case NotificationChannel.Telegram:
                await telegram.SendMessageAsync(recipient, text, cancellationToken);
                break;
            case NotificationChannel.Sms:
                await sms.SendAsync(recipient, text, cancellationToken);
                break;
            case NotificationChannel.Email:
                await email.SendAsync(recipient, subject, text, cancellationToken);
                break;
            default:
                logger.LogInformation("Notification channel {Channel} has no adapter; skipped", channel);
                break;
        }
    }

    private async Task<string> BuildTextAsync(NotificationMessage message, CancellationToken cancellationToken)
    {
        var name = message.Data.GetValueOrDefault("name", "");
        var balance = message.Data.GetValueOrDefault("balance", "");
        var currency = message.Data.GetValueOrDefault("currency", "");
        var days = message.Data.GetValueOrDefault("days", "");
        var dueDate = message.Data.GetValueOrDefault("dueDate", "");

        if (message.Template is "debt_reminder" or "debt_due_soon")
        {
            var reminder = await settings.GetAsync<ReminderSettings>(SettingKeys.Reminder, cancellationToken);
            var template = message.Template == "debt_reminder" ? reminder?.OverdueTemplate : reminder?.DueSoonTemplate;
            if (!string.IsNullOrWhiteSpace(template))
                return template
                    .Replace("{name}", name)
                    .Replace("{balance}", balance)
                    .Replace("{currency}", currency)
                    .Replace("{days}", days)
                    .Replace("{dueDate}", dueDate);
        }

        return message.Template switch
        {
            "debt_reminder" => $"Hurmatli {name}! Do'kondan qarzingiz: {balance} {currency} ({days} kundan beri). Iltimos, to'lovni amalga oshiring.",
            "debt_due_soon" => $"Hurmatli {name}! Do'kondan qarzingiz {balance} {currency} bo'yicha to'lov muddati: {dueDate}. Iltimos, o'z vaqtida to'lang.",
            _ => message.Template
        };
    }

    private static string Subject(string template) => template is "debt_reminder" or "debt_due_soon" ? "Qarz eslatmasi" : "Chek";
}
