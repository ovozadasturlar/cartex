using MediatR;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;

namespace Cartex.Application.Users.Queries;

public record GetUsersQuery(long? ShopId) : IRequest<List<UserDto>>;

public record UserDto(long Id, string FullName, string Username, string RoleName, string ShopName, bool IsActive);

public sealed class GetUsersQueryHandler(IApplicationDbContext db) : IRequestHandler<GetUsersQuery, List<UserDto>>
{
    public async Task<List<UserDto>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        var query = db.Users
            .Include(u => u.Role)
            .Include(u => u.Shop)
            .AsQueryable();

        if (request.ShopId is not null)
            query = query.Where(u => u.ShopId == request.ShopId);

        return await query
            .Select(u => new UserDto(u.Id, u.FullName, u.Username, u.Role.Name, u.Shop.Name, u.IsActive))
            .ToListAsync(cancellationToken);
    }
}
