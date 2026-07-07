using Cartex.Domain.Entities;

namespace Cartex.Application.Common.Security;

public sealed record AccessContext(long UserId, int Level, bool AccessAll, IReadOnlySet<string> Permissions, IReadOnlySet<string> GrantablePermissions, IReadOnlySet<string> AssignableRoles);

public interface IAccessControlService
{
    Task<AccessContext> GetContextAsync(CancellationToken cancellationToken);
    Task EnsureCanCreateRoleAsync(int level, CancellationToken cancellationToken);
    Task EnsureCanManageRoleAsync(Role role, CancellationToken cancellationToken);
    Task EnsureCanGrantAsync(IReadOnlyCollection<long> permissionIds, CancellationToken cancellationToken);
    Task EnsureCanDelegateAsync(IReadOnlyCollection<string> permissionKeys, CancellationToken cancellationToken);
    Task EnsureCanAssignRolesAsync(IReadOnlyCollection<long> roleIds, CancellationToken cancellationToken);
    Task EnsureCanManageUserAsync(User user, CancellationToken cancellationToken);
    Task EnsureAdminRemainsAsync(long excludingUserId, CancellationToken cancellationToken);
}
