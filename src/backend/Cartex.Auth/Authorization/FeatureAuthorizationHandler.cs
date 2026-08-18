using Cartex.Domain.Common;
using Microsoft.AspNetCore.Authorization;

namespace Cartex.Auth.Authorization;

// SOZ-15: feature — tizim holati, foydalanuvchi imtiyozi emas; wildcard ham bo'ysunadi.
public class FeatureAuthorizationHandler(IFeatureStateProvider features) : AuthorizationHandler<FeatureRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, FeatureRequirement requirement)
    {
        foreach (var feature in requirement.Feature.Split('|'))
            if (await features.IsEnabledAsync(feature))
            {
                context.Succeed(requirement);
                return;
            }
    }
}
