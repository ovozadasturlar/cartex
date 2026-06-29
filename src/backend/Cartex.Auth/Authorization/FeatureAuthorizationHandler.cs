using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Microsoft.AspNetCore.Authorization;

namespace Cartex.Auth.Authorization;

public class FeatureAuthorizationHandler(IFeatureStateProvider features) : AuthorizationHandler<FeatureRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, FeatureRequirement requirement)
    {
        if (context.User.Claims.Any(c => c.Type == "permission" && c.Value == AppPermissions.Wildcard))
        {
            context.Succeed(requirement);
            return;
        }

        if (await features.IsEnabledAsync(requirement.Feature))
            context.Succeed(requirement);
    }
}
