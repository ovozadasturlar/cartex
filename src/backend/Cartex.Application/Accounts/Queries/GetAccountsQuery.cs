using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Domain.Common;
using Cartex.Persistence;
using Cartex.Shared.Models.Accounts;

namespace Cartex.Application.Accounts.Queries;

public record GetAccountsQuery : FilteringRequest, IRequest<IReadOnlyCollection<AccountDto>>;

public sealed class GetAccountsQueryHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IPagingMetadataWriter writer) : IRequestHandler<GetAccountsQuery, IReadOnlyCollection<AccountDto>>
{
    public async Task<IReadOnlyCollection<AccountDto>> Handle(GetAccountsQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SortBy))
        {
            request.SortBy = "Id";
            request.Descending = false;
        }
        var query = db.Accounts.AsQueryable();
        if (!currentUser.CanAccessAllBranches)
            query = query.Where(a => a.BranchId == null || currentUser.BranchIds.Contains(a.BranchId.Value));

        return await query
            .ToPagedListAsync(request,
                a => new AccountDto(
                    a.Id,
                    a.Name,
                    a.Type.ToString(),
                    a.Currency,
                    a.Balance,
                    a.Customer != null ? a.Customer.FullName
                        : a.Branch != null ? a.Branch.Name
                        : a.Supplier != null ? a.Supplier.Name
                        : null),
                writer, cancellationToken);
    }
}
