using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Cartex.Auth.Authorization;

public class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        var policy = await base.GetPolicyAsync(policyName);
        if (policy is not null)
            return policy;

        if (policyName.StartsWith("feature:", StringComparison.Ordinal))
            return new AuthorizationPolicyBuilder()
                .AddRequirements(new FeatureRequirement(policyName["feature:".Length..]))
                .Build();

        return new AuthorizationPolicyBuilder()
            .AddRequirements(new PermissionRequirement(policyName))
            .Build();
    }
}
