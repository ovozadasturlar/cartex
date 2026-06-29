using Cartex.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace Cartex.Auth.Authorization;

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var permissions = context.User.Claims
            .Where(c => c.Type == "permission")
            .Select(c => c.Value);

        if (permissions.Contains(requirement.Permission) || permissions.Contains(AppPermissions.Wildcard))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
