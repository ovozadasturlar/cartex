using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;

namespace Cartex.Application.Settings.Queries;

public record TestTelegramQuery(string? Token) : IRequest<TelegramBotInfo>;

public sealed class TestTelegramQueryHandler(ITelegramService telegram, ISettingsService settings, ISecretProtector protector)
    : IRequestHandler<TestTelegramQuery, TelegramBotInfo>
{
    public async Task<TelegramBotInfo> Handle(TestTelegramQuery request, CancellationToken cancellationToken)
    {
        var token = request.Token;
        if (string.IsNullOrWhiteSpace(token))
        {
            var cfg = await settings.GetAsync<TelegramSettings>(SettingKeys.Telegram, cancellationToken);
            if (string.IsNullOrWhiteSpace(cfg?.BotToken))
                return new TelegramBotInfo(false, null);
            token = protector.Unprotect(cfg.BotToken);
        }

        return await telegram.ValidateAsync(token, cancellationToken);
    }
}
