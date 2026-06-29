namespace Cartex.Application.Common.Settings;

public sealed class TelegramSettings
{
    public bool Enabled { get; set; }
    public string? BotToken { get; set; }
    public string? ChatId { get; set; }
}
