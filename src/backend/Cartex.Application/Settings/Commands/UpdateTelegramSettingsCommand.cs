using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Settings;
using Cartex.Persistence;

namespace Cartex.Application.Settings.Commands;

public record UpdateTelegramSettingsCommand(bool Enabled, string? ChatId, string? BotToken, bool ClearToken = false) : ICommand<Unit>;

public sealed class UpdateTelegramSettingsCommandHandler(ISettingsService settings, ISecretProtector protector, IAuditService audit)
    : IRequestHandler<UpdateTelegramSettingsCommand, Unit>
{
    public async Task<Unit> Handle(UpdateTelegramSettingsCommand request, CancellationToken cancellationToken)
    {
        var cfg = await settings.GetAsync<TelegramSettings>(SettingKeys.Telegram, cancellationToken) ?? new();
        cfg.Enabled = request.Enabled;
        cfg.ChatId = request.ChatId;
        if (request.ClearToken)
            cfg.BotToken = null;
        else if (!string.IsNullOrWhiteSpace(request.BotToken))
            cfg.BotToken = protector.Protect(request.BotToken);

        audit.Add("settings", "settings", null, new { section = "telegram" });
        await settings.SetAsync(SettingKeys.Telegram, cfg, cancellationToken);
        return Unit.Value;
    }
}
