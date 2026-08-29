using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Persistence;
using FluentValidation;

namespace Cartex.Application.Settings.Commands;

public record UpdateLoginMethodsSettingsCommand(bool QrEnabled, int QrRefreshSeconds, bool KeyEnabled) : ICommand<Unit>;

public sealed class UpdateLoginMethodsSettingsCommandHandler(ISettingsService settings, IAuditService audit)
    : IRequestHandler<UpdateLoginMethodsSettingsCommand, Unit>
{
    public async Task<Unit> Handle(UpdateLoginMethodsSettingsCommand request, CancellationToken cancellationToken)
    {
        var cfg = new LoginMethodsSettings
        {
            QrEnabled = request.QrEnabled,
            QrRefreshSeconds = request.QrRefreshSeconds,
            KeyEnabled = request.KeyEnabled
        };
        audit.Add("settings", "settings", null, new { section = "loginMethods" });
        await settings.SetAsync(SettingKeys.LoginMethods, cfg, cancellationToken);
        return Unit.Value;
    }
}

public sealed class UpdateLoginMethodsSettingsCommandValidator : AbstractValidator<UpdateLoginMethodsSettingsCommand>
{
    public UpdateLoginMethodsSettingsCommandValidator()
    {
        RuleFor(x => x.QrRefreshSeconds).InclusiveBetween(30, 600);
    }
}
