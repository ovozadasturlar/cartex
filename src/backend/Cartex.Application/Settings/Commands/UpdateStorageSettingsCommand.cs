using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Settings;
using Cartex.Persistence;
using FluentValidation;

namespace Cartex.Application.Settings.Commands;

public record UpdateStorageSettingsCommand(bool Enabled, string Provider, string? Endpoint, string? AccessKey, string? SecretKey, string? Bucket, bool UseSsl) : ICommand<Unit>;

public sealed class UpdateStorageSettingsCommandHandler(ISettingsService settings, ISecretProtector protector, IStorageConnectionTester tester, IAuditService audit)
    : IRequestHandler<UpdateStorageSettingsCommand, Unit>
{
    public async Task<Unit> Handle(UpdateStorageSettingsCommand request, CancellationToken cancellationToken)
    {
        var cfg = await settings.GetAsync<StorageSettings>(SettingKeys.Storage, cancellationToken) ?? new StorageSettings();

        if (request.Enabled && request.Provider == "minio")
            await tester.EnsureReachableAsync(new StorageSettings
            {
                Enabled = true,
                Provider = "minio",
                Endpoint = request.Endpoint?.Trim(),
                AccessKey = request.AccessKey?.Trim(),
                SecretKey = !string.IsNullOrWhiteSpace(request.SecretKey) ? request.SecretKey.Trim() : Reveal(cfg.SecretKey),
                Bucket = request.Bucket?.Trim(),
                UseSsl = request.UseSsl
            }, cancellationToken);

        cfg.Enabled = request.Enabled;
        cfg.Provider = request.Provider;
        cfg.Endpoint = request.Endpoint?.Trim();
        cfg.AccessKey = request.AccessKey?.Trim();
        cfg.Bucket = request.Bucket?.Trim();
        cfg.UseSsl = request.UseSsl;
        if (!string.IsNullOrWhiteSpace(request.SecretKey))
            cfg.SecretKey = protector.Protect(request.SecretKey.Trim());

        audit.Add("settings", "settings", null, new { section = "storage" });
        await settings.SetAsync(SettingKeys.Storage, cfg, cancellationToken);
        return Unit.Value;
    }

    private string? Reveal(string? secret)
    {
        if (string.IsNullOrWhiteSpace(secret)) return secret;
        try { return protector.Unprotect(secret); }
        catch { return secret; }
    }
}

public sealed class UpdateStorageSettingsCommandValidator : AbstractValidator<UpdateStorageSettingsCommand>
{
    public UpdateStorageSettingsCommandValidator()
    {
        RuleFor(x => x.Provider).Must(p => p is "local" or "minio");
        RuleFor(x => x.Endpoint).NotEmpty().When(x => x.Enabled && x.Provider == "minio");
        RuleFor(x => x.Bucket).NotEmpty().When(x => x.Enabled && x.Provider == "minio");
        RuleFor(x => x.Endpoint).MaximumLength(200);
        RuleFor(x => x.AccessKey).MaximumLength(200);
        RuleFor(x => x.SecretKey).MaximumLength(500);
        RuleFor(x => x.Bucket).MaximumLength(100);
    }
}
