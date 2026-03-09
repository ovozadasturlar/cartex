using MediatR;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;

namespace Cartex.Application.Shops.Queries;

public record GetShopsQuery : IRequest<List<ShopDto>>;

public record ShopDto(long Id, string Name, string? Address, string? Phone, decimal CashbackRate, bool IsActive);

public sealed class GetShopsQueryHandler(IApplicationDbContext db) : IRequestHandler<GetShopsQuery, List<ShopDto>>
{
    public async Task<List<ShopDto>> Handle(GetShopsQuery request, CancellationToken cancellationToken)
    {
        return await db.Shops
            .Select(s => new ShopDto(s.Id, s.Name, s.Address, s.Phone, s.CashbackRate, s.IsActive))
            .ToListAsync(cancellationToken);
    }
}
