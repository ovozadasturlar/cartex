using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Persistence;

namespace Cartex.Application.Settings.Commands;

public record UpdateSmsSettingsCommand(
    bool Enabled, string Provider, string? Login, string? Password, string? Sender, string? BaseUrl) : ICommand<Unit>;

public sealed class UpdateSmsSettingsCommandHandler(ISettingsService settings, ISecretProtector protector, IAuditService audit)
    : IRequestHandler<UpdateSmsSettingsCommand, Unit>
{
    public async Task<Unit> Handle(UpdateSmsSettingsCommand request, CancellationToken cancellationToken)
    {
        var cfg = await settings.GetAsync<SmsSettings>(SettingKeys.Sms, cancellationToken) ?? new();
        cfg.Enabled = request.Enabled;
        cfg.Provider = request.Provider;
        cfg.Login = request.Login;
        cfg.Sender = request.Sender;
        cfg.BaseUrl = request.BaseUrl;
        if (!string.IsNullOrWhiteSpace(request.Password))
            cfg.Password = protector.Protect(request.Password);

        audit.Add("settings", "settings", null, new { section = "sms" });
        await settings.SetAsync(SettingKeys.Sms, cfg, cancellationToken);
        return Unit.Value;
    }
}
