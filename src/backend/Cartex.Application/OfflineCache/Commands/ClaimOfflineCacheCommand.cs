using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Persistence;
using FluentValidation;

using Unit = Cartex.Application.Common.Messaging.Unit;

namespace Cartex.Application.OfflineCache.Commands;

public record ClaimOfflineCacheCommand(string DeviceId, string DeviceName) : ICommand<Unit>;

public sealed class ClaimOfflineCacheCommandHandler(ISettingsService settings, IAuditService audit)
    : IRequestHandler<ClaimOfflineCacheCommand, Unit>
{
    public async Task<Unit> Handle(ClaimOfflineCacheCommand request, CancellationToken cancellationToken)
    {
        var cfg = await settings.GetAsync<OfflineCacheSettings>(SettingKeys.OfflineCache, cancellationToken) ?? new OfflineCacheSettings();
        if (cfg.DeviceId is { Length: > 0 } && cfg.DeviceId != request.DeviceId)
            throw new BusinessRuleException($"Oflayn kesh allaqachon \"{cfg.DeviceName}\" qurilmasiga berilgan. Avval o'sha qurilmadan yoki shu yerdan uzing.");

        cfg.DeviceId = request.DeviceId;
        cfg.DeviceName = request.DeviceName;
        cfg.ClaimedAt = DateTime.UtcNow;
        audit.Add("offlineClaim", "settings", null, new { request.DeviceId, request.DeviceName });
        await settings.SetAsync(SettingKeys.OfflineCache, cfg, cancellationToken);
        return Unit.Value;
    }
}

public sealed class ClaimOfflineCacheCommandValidator : AbstractValidator<ClaimOfflineCacheCommand>
{
    public ClaimOfflineCacheCommandValidator()
    {
        RuleFor(x => x.DeviceId).NotEmpty().MaximumLength(64);
        RuleFor(x => x.DeviceName).NotEmpty().MaximumLength(100);
    }
}
