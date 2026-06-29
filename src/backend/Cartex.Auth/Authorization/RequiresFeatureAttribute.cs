using Microsoft.AspNetCore.Authorization;

namespace Cartex.Auth.Authorization;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class RequiresFeatureAttribute(string feature) : AuthorizeAttribute("feature:" + feature)
{
    public string Feature { get; } = feature;
}
