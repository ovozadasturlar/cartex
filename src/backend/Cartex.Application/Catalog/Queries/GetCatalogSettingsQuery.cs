using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Shared.Models.Catalog;

namespace Cartex.Application.Catalog.Queries;

public sealed record GetCatalogSettingsQuery : IRequest<CatalogSettingsDto>;

public sealed class GetCatalogSettingsQueryHandler(ISettingsService settings, ICatalogPackStore packs)
    : IRequestHandler<GetCatalogSettingsQuery, CatalogSettingsDto>
{
    public async Task<CatalogSettingsDto> Handle(GetCatalogSettingsQuery request, CancellationToken cancellationToken)
    {
        var config = await settings.GetAsync<ProductReferenceSettings>(SettingKeys.ProductReference, cancellationToken)
            ?? new ProductReferenceSettings();
        var state = await packs.StateAsync(cancellationToken);

        return new CatalogSettingsDto
        {
            Mode = config.Mode,
            EndpointBaseUrl = config.EndpointBaseUrl,
            ImageBaseUrl = config.ImageBaseUrl,
            Pack = state.Pack,
            LastError = state.Error
        };
    }
}
