using MediatR;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;

namespace Cartex.Application.Accounts.Queries;

public record GetAccountsQuery(string? OwnerType, long? OwnerId) : IRequest<List<AccountDto>>;

public record AccountDto(long Id, string OwnerType, long OwnerId, string Name, string Type, decimal Balance);

public sealed class GetAccountsQueryHandler(IApplicationDbContext db) : IRequestHandler<GetAccountsQuery, List<AccountDto>>
{
    public async Task<List<AccountDto>> Handle(GetAccountsQuery request, CancellationToken cancellationToken)
    {
        var query = db.Accounts.AsQueryable();

        if (request.OwnerType is not null)
            query = query.Where(a => a.OwnerType.ToString() == request.OwnerType);

        if (request.OwnerId is not null)
            query = query.Where(a => a.OwnerId == request.OwnerId);

        return await query
            .Select(a => new AccountDto(
                a.Id,
                a.OwnerType.ToString(),
                a.OwnerId,
                a.Name,
                a.Type.ToString(),
                a.Balance))
            .ToListAsync(cancellationToken);
    }
}
