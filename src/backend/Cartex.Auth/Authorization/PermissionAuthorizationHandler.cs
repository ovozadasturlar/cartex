using Cartex.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace Cartex.Auth.Authorization;

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var permissions = context.User.Claims
            .Where(c => c.Type == "permission")
            .Select(c => c.Value)
            .ToHashSet();

        if (permissions.Contains(AppPermissions.Wildcard) || requirement.Permission.Split('|').Any(permissions.Contains))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
