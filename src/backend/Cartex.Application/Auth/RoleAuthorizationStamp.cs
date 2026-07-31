using System.Security.Cryptography;
using System.Text;
using Cartex.Domain.Entities;

namespace Cartex.Application.Auth;

internal static class RoleAuthorizationStamp
{
    public static string Create(IEnumerable<Role> roles)
    {
        var parts = roles
            .OrderBy(role => role.Id)
            .SelectMany(role =>
            {
                var rolePart = $"r:{role.Id}:{Timestamp(role)}";
                var permissionParts = role.RolePermissions
                    .Where(rolePermission => rolePermission.Permission.IsEnabled)
                    .OrderBy(rolePermission => rolePermission.PermissionId)
                    .Select(rolePermission =>
                        $"p:{rolePermission.PermissionId}:{Timestamp(rolePermission.Permission)}");
                return new[] { rolePart }.Concat(permissionParts);
            });

        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|', parts))));
    }

    private static long Timestamp(Cartex.Domain.Common.AuditableEntity entity) =>
        (entity.UpdatedAt ?? entity.CreatedAt).Ticks;
}
