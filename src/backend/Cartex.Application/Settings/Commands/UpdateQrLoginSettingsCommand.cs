using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Settings;
using Cartex.Persistence;
using FluentValidation;

namespace Cartex.Application.Settings.Commands;

public record UpdateQrLoginSettingsCommand(int RefreshSeconds) : ICommand<Unit>;

public sealed class UpdateQrLoginSettingsCommandHandler(ISettingsService settings, IAuditService audit)
    : IRequestHandler<UpdateQrLoginSettingsCommand, Unit>
{
    public async Task<Unit> Handle(UpdateQrLoginSettingsCommand request, CancellationToken cancellationToken)
    {
        var cfg = await settings.GetAsync<QrLoginSettings>(SettingKeys.QrLogin, cancellationToken) ?? new();
        cfg.RefreshSeconds = request.RefreshSeconds;
        audit.Add("settings", "settings", null, new { section = "qrLogin" });
        await settings.SetAsync(SettingKeys.QrLogin, cfg, cancellationToken);
        return Unit.Value;
    }
}

public sealed class UpdateQrLoginSettingsCommandValidator : AbstractValidator<UpdateQrLoginSettingsCommand>
{
    public UpdateQrLoginSettingsCommandValidator()
    {
        RuleFor(x => x.RefreshSeconds).InclusiveBetween(30, 600);
    }
}
