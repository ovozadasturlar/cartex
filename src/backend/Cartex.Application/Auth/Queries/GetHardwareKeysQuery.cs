using Cartex.Application.Common.Messaging;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Auth.Queries;

public record HardwareKeyDto(long Id, long UserId, string Username, string FullName, string Serial, DateTime IssuedAt, DateTime? RevokedAt);

public record GetHardwareKeysQuery : IRequest<IReadOnlyList<HardwareKeyDto>>;

public sealed class GetHardwareKeysQueryHandler(IApplicationDbContext db) : IRequestHandler<GetHardwareKeysQuery, IReadOnlyList<HardwareKeyDto>>
{
    public async Task<IReadOnlyList<HardwareKeyDto>> Handle(GetHardwareKeysQuery request, CancellationToken cancellationToken) =>
        await db.HardwareKeys
            .OrderByDescending(k => k.Id)
            .Select(k => new HardwareKeyDto(k.Id, k.UserId, k.User.Username, k.User.FullName, k.Serial, k.CreatedAt, k.RevokedAt))
            .ToListAsync(cancellationToken);
}
