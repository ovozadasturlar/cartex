using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;

namespace Cartex.Application.Settings.Queries;

public record StorageSettingsDto(bool Enabled, string Provider, string? Endpoint, string? AccessKey, string? Bucket, bool UseSsl, bool HasSecretKey, int SecretKeyLength = 0);

public record GetStorageSettingsQuery : IRequest<StorageSettingsDto>;

public sealed class GetStorageSettingsQueryHandler(ISettingsService settings, ISecretProtector protector)
    : IRequestHandler<GetStorageSettingsQuery, StorageSettingsDto>
{
    public async Task<StorageSettingsDto> Handle(GetStorageSettingsQuery request, CancellationToken cancellationToken)
    {
        var cfg = await settings.GetAsync<StorageSettings>(SettingKeys.Storage, cancellationToken) ?? new StorageSettings();
        return new StorageSettingsDto(cfg.Enabled, cfg.Provider, cfg.Endpoint, cfg.AccessKey, cfg.Bucket, cfg.UseSsl, !string.IsNullOrEmpty(cfg.SecretKey), SecretLength(cfg.SecretKey));
    }

    private int SecretLength(string? secret)
    {
        if (string.IsNullOrEmpty(secret)) return 0;
        try { return protector.Unprotect(secret)?.Length ?? 0; }
        catch { return secret.Length; }
    }
}
