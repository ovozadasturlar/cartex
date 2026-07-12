using Microsoft.AspNetCore.Authorization;

namespace Cartex.Auth.Authorization;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class RequiresFeatureAttribute(params string[] features) : AuthorizeAttribute("feature:" + string.Join('|', features))
{
    public string Feature { get; } = string.Join('|', features);
}
