namespace Cartex.Application.Common.Settings;

public sealed class SmsSettings
{
    public bool Enabled { get; set; }
    public string Provider { get; set; } = "device";
    public string? Login { get; set; }
    public string? Password { get; set; }
    public string? Sender { get; set; }
    public string? BaseUrl { get; set; }
    public string FallbackProvider { get; set; } = "none";
    public int FallbackAfterMinutes { get; set; } = 30;
    public bool DebtReminderEnabled { get; set; } = true;
    public bool ReceiptLinkEnabled { get; set; }
    public bool PromotionEnabled { get; set; }
    public bool ManualEnabled { get; set; } = true;
    public bool SendReceiptOnSale { get; set; }
    public bool TestMode { get; set; } = true;
    public List<string> TestAllowedNumbers { get; set; } = [];
    public int DebtReminderStickyWaitMinutes { get; set; } = 15;
    public int ReceiptLinkStickyWaitMinutes { get; set; }
    public int PromotionStickyWaitMinutes { get; set; } = 60;
    public int ManualStickyWaitMinutes { get; set; }
    public bool QuietHoursEnabled { get; set; } = true;
    public string SendWindowStart { get; set; } = "09:00";
    public string SendWindowEnd { get; set; } = "21:00";
    public string DebtReminderTemplate { get; set; } = "{store}: Hurmatli {name}, qarzingiz {balance} {currency}.";
    public string ReceiptLinkTemplate { get; set; } = "{store}: chekingiz {link}";
    public string PromotionTemplate { get; set; } = "{store}: {text}";
    public string ManualTemplate { get; set; } = "{store}: {text}";
}
