using Cartex.Domain.Common;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Store.Queries;

public record StoreProfileDto(long Id, string FullName, string? Phone, string? Language);

public record GetStoreProfileQuery : IRequest<StoreProfileDto>;

public sealed class GetStoreProfileQueryHandler(IApplicationDbContext db, ICurrentCustomer currentCustomer)
    : IRequestHandler<GetStoreProfileQuery, StoreProfileDto>
{
    public async Task<StoreProfileDto> Handle(GetStoreProfileQuery request, CancellationToken cancellationToken)
    {
        var customerId = currentCustomer.CustomerId ?? throw new UnauthorizedAccessException("Not authenticated.");
        return await db.Customers
            .Where(c => c.Id == customerId)
            .Select(c => new StoreProfileDto(c.Id, c.FullName, c.Phone, c.PreferredLanguage))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new UnauthorizedAccessException("Not authenticated.");
    }
}
