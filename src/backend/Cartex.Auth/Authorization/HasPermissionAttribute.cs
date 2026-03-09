using Microsoft.AspNetCore.Authorization;

namespace Cartex.Auth.Authorization;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class HasPermissionAttribute(string permission) : AuthorizeAttribute(permission)
{
    public string Permission { get; } = permission;
}
