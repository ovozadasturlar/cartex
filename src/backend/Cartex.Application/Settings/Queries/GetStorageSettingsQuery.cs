using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Settings;

namespace Cartex.Application.Settings.Queries;

public record StorageSettingsDto(bool Enabled, string Provider, string? Endpoint, string? AccessKey, string? Bucket, bool UseSsl, bool HasSecretKey);

public record GetStorageSettingsQuery : IRequest<StorageSettingsDto>;

public sealed class GetStorageSettingsQueryHandler(ISettingsService settings)
    : IRequestHandler<GetStorageSettingsQuery, StorageSettingsDto>
{
    public async Task<StorageSettingsDto> Handle(GetStorageSettingsQuery request, CancellationToken cancellationToken)
    {
        var cfg = await settings.GetAsync<StorageSettings>(SettingKeys.Storage, cancellationToken) ?? new StorageSettings();
        return new StorageSettingsDto(cfg.Enabled, cfg.Provider, cfg.Endpoint, cfg.AccessKey, cfg.Bucket, cfg.UseSsl, !string.IsNullOrEmpty(cfg.SecretKey));
    }
}
