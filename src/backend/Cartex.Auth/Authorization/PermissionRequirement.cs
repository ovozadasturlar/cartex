using Microsoft.AspNetCore.Authorization;

namespace Cartex.Auth.Authorization;

public class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
