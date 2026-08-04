using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Settings;
using Cartex.Shared.Models.Settings;

namespace Cartex.Application.Settings.Queries;

public record GetBarcodeLabelSettingsQuery : IRequest<BarcodeLabelSettingsDto>;

public sealed class GetBarcodeLabelSettingsQueryHandler(ISettingsService settings)
    : IRequestHandler<GetBarcodeLabelSettingsQuery, BarcodeLabelSettingsDto>
{
    public async Task<BarcodeLabelSettingsDto> Handle(GetBarcodeLabelSettingsQuery request, CancellationToken cancellationToken)
    {
        var value = await settings.GetAsync<BarcodeLabelSettings>(SettingKeys.BarcodeLabel, cancellationToken) ?? new();
        return new BarcodeLabelSettingsDto(
            value.DefaultWithPrice,
            value.AllowPriceOverride,
            value.ShowSku,
            value.NameLines,
            value.CurrencyDisplay,
            value.CurrencyCase,
            value.PriceCurrencyMode);
    }
}
