using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Persistence;
using MediatR;

namespace Cartex.Application.Accounts.Queries;

public record GetAccountsQuery : FilteringRequest, IRequest<IReadOnlyCollection<AccountDto>>;

public record AccountDto(long Id, string OwnerType, long OwnerId, string Name, string Type, decimal Balance);

public sealed class GetAccountsQueryHandler(
    IApplicationDbContext db,
    IPagingMetadataWriter writer) : IRequestHandler<GetAccountsQuery, IReadOnlyCollection<AccountDto>>
{
    public async Task<IReadOnlyCollection<AccountDto>> Handle(GetAccountsQuery request, CancellationToken cancellationToken)
    {
        return await db.Accounts
            .ToPagedListAsync(request,
                a => new AccountDto(
                    a.Id,
                    a.OwnerType.ToString(),
                    a.OwnerId,
                    a.Name,
                    a.Type.ToString(),
                    a.Balance),
                writer, cancellationToken);
    }
}
