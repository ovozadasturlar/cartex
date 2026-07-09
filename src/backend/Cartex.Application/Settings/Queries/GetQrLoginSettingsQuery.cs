using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Application.Common.Messaging;

namespace Cartex.Application.Settings.Queries;

public record QrLoginSettingsDto(int RefreshSeconds);

public record GetQrLoginSettingsQuery : IRequest<QrLoginSettingsDto>;

public sealed class GetQrLoginSettingsQueryHandler(ISettingsService settings) : IRequestHandler<GetQrLoginSettingsQuery, QrLoginSettingsDto>
{
    public async Task<QrLoginSettingsDto> Handle(GetQrLoginSettingsQuery request, CancellationToken cancellationToken)
    {
        var cfg = await settings.GetAsync<QrLoginSettings>(SettingKeys.QrLogin, cancellationToken) ?? new();
        return new QrLoginSettingsDto(cfg.RefreshSeconds);
    }
}
