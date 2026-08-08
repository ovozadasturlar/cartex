using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Rates.Queries;

public record CurrencyDto(string Code, string Name, bool IsSystem, bool IsEnabled, bool IsDefault, bool IsBase, decimal? Rate, DateTime? RateAt, string Symbol, string SymbolPosition, int DecimalDigits);

public record GetCurrenciesQuery(bool OnlyEnabled = false) : IRequest<IReadOnlyCollection<CurrencyDto>>;

public sealed class GetCurrenciesQueryHandler(IApplicationDbContext db) : IRequestHandler<GetCurrenciesQuery, IReadOnlyCollection<CurrencyDto>>
{
    public async Task<IReadOnlyCollection<CurrencyDto>> Handle(GetCurrenciesQuery request, CancellationToken cancellationToken)
    {
        var baseCurrency = await db.Businesses.Select(b => b.Currency).FirstAsync(cancellationToken);
        var currencies = await db.Currencies
            .Where(c => !request.OnlyEnabled || c.IsEnabled)
            .OrderByDescending(c => c.Code == baseCurrency)
            .ThenBy(c => c.Code)
            .ToListAsync(cancellationToken);

        var rates = await db.ExchangeRates
            .GroupBy(r => r.Code)
            .Select(g => g.OrderByDescending(r => r.EffectiveAt).First())
            .ToListAsync(cancellationToken);
        var rateByCode = rates.ToDictionary(r => r.Code);

        return currencies.Select(c =>
        {
            var rate = rateByCode.GetValueOrDefault(c.Code);
            return new CurrencyDto(c.Code, c.Name, c.IsSystem, c.IsEnabled, c.IsDefault, c.Code == baseCurrency,
                c.Code == baseCurrency ? 1m : rate?.Rate, rate?.EffectiveAt, c.Symbol, c.SymbolPosition, c.DecimalDigits);
        }).ToList();
    }
}
