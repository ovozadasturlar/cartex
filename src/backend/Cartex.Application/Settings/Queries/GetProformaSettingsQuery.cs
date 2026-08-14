using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Shared.Models.Settings;

namespace Cartex.Application.Settings.Queries;

public record GetProformaSettingsQuery : IRequest<ProformaSettingsDto>;

public sealed class GetProformaSettingsQueryHandler(ISettingsService settings)
    : IRequestHandler<GetProformaSettingsQuery, ProformaSettingsDto>
{
    public async Task<ProformaSettingsDto> Handle(GetProformaSettingsQuery request, CancellationToken cancellationToken)
    {
        var cfg = await settings.GetAsync<ProformaSettings>(SettingKeys.Proforma, cancellationToken) ?? new ProformaSettings();
        return new ProformaSettingsDto(
            cfg.HeaderText,
            cfg.FooterText,
            cfg.PaperWidth,
            cfg.PaperFormat,
            cfg.ShowBusinessName,
            cfg.ShowAddress,
            cfg.ShowPhone,
            cfg.ShowSeller,
            cfg.ShowCustomer,
            cfg.ShowNote,
            cfg.ShowCartCode);
    }
}
