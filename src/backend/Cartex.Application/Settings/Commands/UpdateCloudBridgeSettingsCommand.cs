using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Settings;
using Cartex.Persistence;
using FluentValidation;

namespace Cartex.Application.Settings.Commands;

public record UpdateCloudBridgeSettingsCommand(bool Enabled, string? GatewayUrl, string? LicenseKey) : ICommand<Unit>;

public sealed class UpdateCloudBridgeSettingsCommandHandler(ISettingsService settings, ISecretProtector protector, IAuditService audit)
    : IRequestHandler<UpdateCloudBridgeSettingsCommand, Unit>
{
    public async Task<Unit> Handle(UpdateCloudBridgeSettingsCommand request, CancellationToken cancellationToken)
    {
        var cfg = await settings.GetAsync<CloudBridgeSettings>(SettingKeys.CloudBridge, cancellationToken) ?? new CloudBridgeSettings();
        cfg.Enabled = request.Enabled;
        cfg.GatewayUrl = request.GatewayUrl?.Trim();
        if (!string.IsNullOrWhiteSpace(request.LicenseKey))
            cfg.LicenseKey = protector.Protect(request.LicenseKey.Trim());

        audit.Add("settings", "settings", null, new { section = "cloudBridge" });
        await settings.SetAsync(SettingKeys.CloudBridge, cfg, cancellationToken);
        return Unit.Value;
    }
}

public sealed class UpdateCloudBridgeSettingsCommandValidator : AbstractValidator<UpdateCloudBridgeSettingsCommand>
{
    public UpdateCloudBridgeSettingsCommandValidator()
    {
        RuleFor(x => x.GatewayUrl).NotEmpty().When(x => x.Enabled);
        RuleFor(x => x.GatewayUrl).MaximumLength(200);
        RuleFor(x => x.LicenseKey).MaximumLength(500);
    }
}
