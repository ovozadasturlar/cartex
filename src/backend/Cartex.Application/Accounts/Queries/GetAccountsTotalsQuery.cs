using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Models;
using Cartex.Domain.Common;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Cartex.Shared.Models.Accounts;

namespace Cartex.Application.Accounts.Queries;

public record GetAccountsTotalsQuery : FilteringRequest, IRequest<AccountsTotalsDto>;

public sealed class GetAccountsTotalsQueryHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser) : IRequestHandler<GetAccountsTotalsQuery, AccountsTotalsDto>
{
    public async Task<AccountsTotalsDto> Handle(GetAccountsTotalsQuery request, CancellationToken cancellationToken)
    {
        var baseCode = await db.Businesses.Select(b => b.Currency).FirstAsync(cancellationToken);
        var query = db.Accounts.AsFilterable(request);
        if (!currentUser.CanAccessAllBranches)
            query = query.Where(a => a.BranchId == null || currentUser.BranchIds.Contains(a.BranchId.Value));
        return new AccountsTotalsDto(
            await query.CountAsync(cancellationToken),
            await query.SumAsync(a => (decimal?)(a.Balance * (a.Currency == baseCode ? 1m
                : db.ExchangeRates.Where(r => r.Code == a.Currency).OrderByDescending(r => r.EffectiveAt).Select(r => r.Rate).FirstOrDefault())), cancellationToken) ?? 0);
    }
}
