using Cartex.Application.Common.Messaging;
using Cartex.Domain.Common;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Store.Queries;

public record StoreBalanceDto(decimal Debt, decimal Bonus, string Currency);

public record GetStoreBalanceQuery : IRequest<StoreBalanceDto>;

public sealed class GetStoreBalanceQueryHandler(IApplicationDbContext db, ICurrentCustomer currentCustomer)
    : IRequestHandler<GetStoreBalanceQuery, StoreBalanceDto>
{
    public async Task<StoreBalanceDto> Handle(GetStoreBalanceQuery request, CancellationToken cancellationToken)
    {
        var customerId = currentCustomer.CustomerId ?? throw new UnauthorizedAccessException("Not authenticated.");
        var baseCode = await db.Businesses.Select(b => b.Currency).FirstAsync(cancellationToken);

        var debt = await db.Accounts
            .Where(a => a.CustomerId == customerId && a.Type == AccountType.Debt)
            .SumAsync(a => a.Balance * (a.Currency == baseCode ? 1m
                : db.ExchangeRates.Where(r => r.Code == a.Currency).OrderByDescending(r => r.EffectiveAt).Select(r => r.Rate).FirstOrDefault()), cancellationToken);

        var bonus = await db.Accounts
            .Where(a => a.CustomerId == customerId && a.Type == AccountType.Bonus)
            .SumAsync(a => a.Balance, cancellationToken);

        return new StoreBalanceDto(debt, bonus, baseCode);
    }
}
