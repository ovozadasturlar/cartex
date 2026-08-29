using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Cartex.Shared.Models.Auth;

namespace Cartex.Application.Auth.Queries;

public record GetHardwareKeysQuery : IRequest<IReadOnlyList<HardwareKeyDto>>;

public sealed class GetHardwareKeysQueryHandler(IApplicationDbContext db) : IRequestHandler<GetHardwareKeysQuery, IReadOnlyList<HardwareKeyDto>>
{
    public async Task<IReadOnlyList<HardwareKeyDto>> Handle(GetHardwareKeysQuery request, CancellationToken cancellationToken) =>
        await db.HardwareKeys
            .OrderByDescending(k => k.Id)
            .Select(k => new HardwareKeyDto(k.Id, k.UserId, k.User.Username, k.User.FullName, k.Serial, k.CreatedAt, k.IsEnabled, k.RevokedAt))
            .ToListAsync(cancellationToken);
}
