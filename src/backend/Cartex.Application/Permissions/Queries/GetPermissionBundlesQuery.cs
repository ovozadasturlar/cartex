using Cartex.Application.Common.Messaging;
using Cartex.Domain.Authorization;

namespace Cartex.Application.Permissions.Queries;

public record PermissionBundleDto(
    string Key,
    string Description,
    IReadOnlyList<string> Permissions);

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
