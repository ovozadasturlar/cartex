using Cartex.Domain.Authorization;
using Cartex.Shared.Models.Permissions;

namespace Cartex.Application.Permissions.Queries;

public record GetPermissionBundlesQuery : IRequest<IReadOnlyList<PermissionBundleDto>>;

public sealed class GetPermissionBundlesQueryHandler
    : IRequestHandler<GetPermissionBundlesQuery, IReadOnlyList<PermissionBundleDto>>
{
    public Task<IReadOnlyList<PermissionBundleDto>> Handle(
        GetPermissionBundlesQuery request,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<PermissionBundleDto> result =
        [
            .. AppPermissions.Bundles.Values.Select(bundle =>
                new PermissionBundleDto(
                    bundle.Key,
                    bundle.Description,
                    [.. bundle.Permissions]))
        ];
        return Task.FromResult(result);
    }
}
