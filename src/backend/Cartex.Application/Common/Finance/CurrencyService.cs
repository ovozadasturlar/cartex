using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Common.Finance;

public interface ICurrencyService
{
    Task<string> BaseAsync(CancellationToken cancellationToken);
    Task<decimal> RateAsync(string code, CancellationToken cancellationToken);
    Task<bool> IsPricingMulticurrencyAsync(CancellationToken cancellationToken);
    Task<bool> IsSalesMulticurrencyAsync(CancellationToken cancellationToken);
    Task EnsurePricingAllowedAsync(string? code, CancellationToken cancellationToken);
    Task EnsureSalesAllowedAsync(string? code, CancellationToken cancellationToken);
}

public sealed class CurrencyService(IApplicationDbContext db, IFeatureStateProvider features) : ICurrencyService
{
    private string? _base;

    public async Task<string> BaseAsync(CancellationToken cancellationToken) =>
        _base ??= await db.Businesses.Select(b => b.Currency).FirstAsync(cancellationToken);

    public async Task<decimal> RateAsync(string code, CancellationToken cancellationToken)
    {
        if (code == await BaseAsync(cancellationToken))
            return 1m;

        var rate = await db.ExchangeRates
            .Where(r => r.Code == code)
            .OrderByDescending(r => r.EffectiveAt)
            .Select(r => (decimal?)r.Rate)
            .FirstOrDefaultAsync(cancellationToken);

        return rate ?? throw new BusinessRuleException($"Kurs kiritilmagan: {code}");
    }

    public Task<bool> IsPricingMulticurrencyAsync(CancellationToken cancellationToken) =>
        features.IsEnabledAsync(FeatureCatalog.PricingMulticurrency, cancellationToken);

    public Task<bool> IsSalesMulticurrencyAsync(CancellationToken cancellationToken) =>
        features.IsEnabledAsync(FeatureCatalog.SalesMulticurrency, cancellationToken);

    public Task EnsurePricingAllowedAsync(string? code, CancellationToken cancellationToken) =>
        EnsureAllowedAsync(code, FeatureCatalog.PricingMulticurrency, cancellationToken);

    public Task EnsureSalesAllowedAsync(string? code, CancellationToken cancellationToken) =>
        EnsureAllowedAsync(code, FeatureCatalog.SalesMulticurrency, cancellationToken);

    private async Task EnsureAllowedAsync(string? code, string feature, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code) || string.Equals(code, await BaseAsync(cancellationToken), StringComparison.OrdinalIgnoreCase))
            return;
        if (!await features.IsEnabledAsync(feature, cancellationToken))
            throw new BusinessRuleException("Ko'p valyuta rejimi o'chirilgan.");

        var normalized = code.Trim().ToUpperInvariant();
        if (!await db.Currencies.AnyAsync(c => c.Code == normalized && c.IsEnabled, cancellationToken))
            throw new BusinessRuleException($"Valyuta faol emas: {normalized}");
        await RateAsync(normalized, cancellationToken);
    }
}
