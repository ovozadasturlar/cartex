using Microsoft.AspNetCore.Authorization;

namespace Cartex.Auth.Authorization;

public class FeatureRequirement(string feature) : IAuthorizationRequirement
{
    public string Feature { get; } = feature;
}
