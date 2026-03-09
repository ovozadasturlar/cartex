using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Persistence;
using MediatR;

namespace Cartex.Application.Permissions.Queries;

public record GetPermissionsQuery : FilteringRequest, IRequest<IReadOnlyCollection<PermissionDto>>;

public record PermissionDto(long Id, string Name, string? Description, bool IsEnabled);

public sealed class GetPermissionsQueryHandler(
    IApplicationDbContext db,
    IPagingMetadataWriter writer) : IRequestHandler<GetPermissionsQuery, IReadOnlyCollection<PermissionDto>>
{
    public async Task<IReadOnlyCollection<PermissionDto>> Handle(GetPermissionsQuery request, CancellationToken cancellationToken)
    {
        return await db.Permissions
            .ToPagedListAsync(request,
                p => new PermissionDto(p.Id, p.Name, p.Description, p.IsEnabled),
                writer, cancellationToken);
    }
}
