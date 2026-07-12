using Microsoft.AspNetCore.Authorization;

namespace Cartex.Auth.Authorization;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class HasPermissionAttribute(params string[] permissions) : AuthorizeAttribute(string.Join('|', permissions))
{
    public string Permission { get; } = string.Join('|', permissions);
}
