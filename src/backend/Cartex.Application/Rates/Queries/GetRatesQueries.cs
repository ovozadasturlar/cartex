using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;

namespace Cartex.Application.Rates.Queries;

public record RateDto(string Code, decimal Rate, DateTime EffectiveAt, string Source);

public record GetCurrentRatesQuery : IRequest<IReadOnlyCollection<RateDto>>;

public sealed class GetCurrentRatesQueryHandler(IApplicationDbContext db) : IRequestHandler<GetCurrentRatesQuery, IReadOnlyCollection<RateDto>>
{
    public async Task<IReadOnlyCollection<RateDto>> Handle(GetCurrentRatesQuery request, CancellationToken cancellationToken)
    {
        var rates = await db.ExchangeRates
            .GroupBy(r => r.Code)
            .Select(g => g.OrderByDescending(r => r.EffectiveAt).First())
            .ToListAsync(cancellationToken);

        return rates.Select(r => new RateDto(r.Code, r.Rate, r.EffectiveAt, r.Source)).ToList();
    }
}

public record GetRateHistoryQuery(string Code) : IRequest<IReadOnlyCollection<RateDto>>;

public sealed class GetRateHistoryQueryHandler(IApplicationDbContext db) : IRequestHandler<GetRateHistoryQuery, IReadOnlyCollection<RateDto>>
{
    public async Task<IReadOnlyCollection<RateDto>> Handle(GetRateHistoryQuery request, CancellationToken cancellationToken)
    {
        return await db.ExchangeRates
            .Where(r => r.Code == request.Code)
            .OrderByDescending(r => r.EffectiveAt)
            .Take(50)
            .Select(r => new RateDto(r.Code, r.Rate, r.EffectiveAt, r.Source))
            .ToListAsync(cancellationToken);
    }
}
