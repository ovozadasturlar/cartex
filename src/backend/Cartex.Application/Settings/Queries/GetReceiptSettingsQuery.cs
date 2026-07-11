using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Settings;

namespace Cartex.Application.Settings.Queries;

public record ReceiptSettingsDto(string? HeaderText, string? FooterText, int PaperWidth, string PaperFormat = "Thermal");

public record GetReceiptSettingsQuery : IRequest<ReceiptSettingsDto>;

public sealed class GetReceiptSettingsQueryHandler(ISettingsService settings)
    : IRequestHandler<GetReceiptSettingsQuery, ReceiptSettingsDto>
{
    public async Task<ReceiptSettingsDto> Handle(GetReceiptSettingsQuery request, CancellationToken cancellationToken)
    {
        var cfg = await settings.GetAsync<ReceiptSettings>(SettingKeys.Receipt, cancellationToken) ?? new ReceiptSettings();
        return new ReceiptSettingsDto(cfg.HeaderText, cfg.FooterText, cfg.PaperWidth, cfg.PaperFormat);
    }
}
