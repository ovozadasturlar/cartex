using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Settings;
using Cartex.Persistence;
using Cartex.Application.Common.Messaging;

namespace Cartex.Application.Settings.Commands;

public record UpdateEmailSettingsCommand(
    bool Enabled, string? Host, int Port, bool UseSsl,
    string? Username, string? Password, string? FromAddress, string? FromName) : ICommand<Unit>;

public sealed class UpdateEmailSettingsCommandHandler(ISettingsService settings, ISecretProtector protector, IAuditService audit)
    : IRequestHandler<UpdateEmailSettingsCommand, Unit>
{
    public async Task<Unit> Handle(UpdateEmailSettingsCommand request, CancellationToken cancellationToken)
    {
        var cfg = await settings.GetAsync<EmailSettings>(SettingKeys.Email, cancellationToken) ?? new();
        cfg.Enabled = request.Enabled;
        cfg.Host = request.Host;
        cfg.Port = request.Port;
        cfg.UseSsl = request.UseSsl;
        cfg.Username = request.Username;
        cfg.FromAddress = request.FromAddress;
        cfg.FromName = request.FromName;
        if (!string.IsNullOrWhiteSpace(request.Password))
            cfg.Password = protector.Protect(request.Password);

        audit.Add("settings", "settings", null, new { section = "email" });
        await settings.SetAsync(SettingKeys.Email, cfg, cancellationToken);
        return Unit.Value;
    }
}
