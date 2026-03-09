using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Persistence;
using MediatR;

namespace Cartex.Application.Shops.Queries;

public record GetShopsQuery : FilteringRequest, IRequest<IReadOnlyCollection<ShopDto>>;

public record ShopDto(long Id, string Name, string? Address, string? Phone, decimal CashbackRate, bool IsActive);

public sealed class GetShopsQueryHandler(
    IApplicationDbContext db,
    IPagingMetadataWriter writer) : IRequestHandler<GetShopsQuery, IReadOnlyCollection<ShopDto>>
{
    public async Task<IReadOnlyCollection<ShopDto>> Handle(GetShopsQuery request, CancellationToken cancellationToken)
    {
        return await db.Shops
            .Select(s => new ShopDto(s.Id, s.Name, s.Address, s.Phone, s.CashbackRate, s.IsActive))
            .ToPagedListAsync(request, writer, cancellationToken);
    }
}
