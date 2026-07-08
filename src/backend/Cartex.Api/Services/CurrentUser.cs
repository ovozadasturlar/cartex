using Cartex.Domain.Authorization;
using Cartex.Domain.Common;

namespace Cartex.Api.Services;

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private System.Security.Claims.ClaimsPrincipal? User => accessor.HttpContext?.User;

    public bool IsAuthenticated => (User?.Identity?.IsAuthenticated ?? false) && User?.FindFirst("userId") is not null;

    public long? UserId => GetLong("userId");
    public long? BusinessId => GetLong("businessId");
    public long? DefaultBranchId => GetLong("defaultBranchId");

    public IReadOnlyCollection<long> BranchIds =>
        User?.FindAll("branchId").Select(c => long.Parse(c.Value)).ToArray() ?? [];

    public bool CanAccessAllBranches =>
        User?.FindAll("permission").Any(c => c.Value == AppPermissions.Branches.ViewAll || c.Value == AppPermissions.Wildcard) ?? false;

    public bool HasPermission(string permission) =>
        User?.FindAll("permission").Any(c => c.Value == permission || c.Value == AppPermissions.Wildcard) ?? false;

    private long? GetLong(string claimType)
    {
        var value = User?.FindFirst(claimType)?.Value;
        return long.TryParse(value, out var result) ? result : null;
    }
}
