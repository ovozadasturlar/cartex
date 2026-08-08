using Cartex.Domain.Enums;

namespace Cartex.Application.Common.Settings;

public enum ReceiptDeliveryFormat
{
    Auto,
    Link,
    Pdf,
    Text
}

public sealed class NotificationSettings
{
    public List<NotificationChannel> Channels { get; set; } = [];
    public bool CopyToAdmin { get; set; }
    public string? PublicBaseUrl { get; set; }
    public ReceiptDeliveryFormat TelegramFormat { get; set; }
    public ReceiptDeliveryFormat EmailFormat { get; set; }
}

public static class ReceiptDeliveryPolicy
{
    public static string Resolve(NotificationChannel channel, NotificationSettings cfg)
    {
        var hasUrl = !string.IsNullOrWhiteSpace(cfg.PublicBaseUrl);
        if (channel == NotificationChannel.Sms)
            return hasUrl ? "link" : "text";

        var format = channel == NotificationChannel.Telegram ? cfg.TelegramFormat : cfg.EmailFormat;
        return format switch
        {
            ReceiptDeliveryFormat.Pdf => "pdf",
            ReceiptDeliveryFormat.Text => "text",
            ReceiptDeliveryFormat.Link => hasUrl ? "link" : "pdf",
            _ => hasUrl ? "link" : "pdf"
        };
    }
}
