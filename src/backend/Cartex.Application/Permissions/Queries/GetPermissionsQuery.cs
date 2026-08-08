using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Domain.Authorization;
using Cartex.Persistence;

namespace Cartex.Application.Permissions.Queries;

public record GetPermissionsQuery : FilteringRequest, IRequest<IReadOnlyCollection<PermissionDto>>;

public record PermissionDto(long Id, string Name, string? Description, bool IsEnabled)
{
    public IReadOnlyList<string> DependsOn { get; init; } = [];
}

public sealed class GetPermissionsQueryHandler(
    IApplicationDbContext db,
    IPagingMetadataWriter writer) : IRequestHandler<GetPermissionsQuery, IReadOnlyCollection<PermissionDto>>
{
    public async Task<IReadOnlyCollection<PermissionDto>> Handle(GetPermissionsQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SortBy))
        {
            request.SortBy = "Id";
            request.Descending = false;
        }
        var permissions = await db.Permissions
            .ToPagedListAsync(request,
                p => new PermissionDto(p.Id, p.Name, p.Description, p.IsEnabled),
                writer, cancellationToken);

        return permissions.Select(p => p with { DependsOn = PermissionDependencies.DependsOn(p.Name) }).ToList();
    }
}
