using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;

namespace Cartex.Application.Business.Queries;

public record GetBusinessQuery : IRequest<BusinessDto>;

public record BusinessDto(
    string Name,
    string? LegalName,
    string Currency,
    bool IsOnboarded,
    string? Phone,
    string? Address,
    string? LogoImageKey,
    bool Multicurrency,
    string? Telegram = null,
    string? Website = null,
    bool PricingMulticurrency = false,
    bool SalesMulticurrency = false,
    string? MonochromeLogoImageKey = null);

public sealed class GetBusinessQueryHandler(IApplicationDbContext db, ISettingsService settings, IFeatureStateProvider features)
    : IRequestHandler<GetBusinessQuery, BusinessDto>
{
    public async Task<BusinessDto> Handle(GetBusinessQuery request, CancellationToken cancellationToken)
    {
        var business = await db.Businesses.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Business not found.");
        var onboarded = await settings.GetAsync<bool>(SettingKeys.Onboarded, cancellationToken);
        var multicurrency = await features.IsEnabledAsync(FeatureCatalog.Multicurrency, cancellationToken);
        var pricingMulticurrency = await features.IsEnabledAsync(FeatureCatalog.PricingMulticurrency, cancellationToken);
        var salesMulticurrency = await features.IsEnabledAsync(FeatureCatalog.SalesMulticurrency, cancellationToken);
        return new BusinessDto(
            business.Name,
            business.LegalName,
            business.Currency,
            onboarded,
            business.Phone,
            business.Address,
            business.LogoImageKey,
            multicurrency,
            business.Telegram,
            business.Website,
            pricingMulticurrency,
            salesMulticurrency,
            business.MonochromeLogoImageKey);
    }
}
