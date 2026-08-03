using Cartex.Domain.Authorization;
using Cartex.Domain.Common;

namespace Cartex.Api.Services;

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private static readonly HashSet<string> KnownClients = ["desktop", "web", "mobile", "tma", "mirror"];

    private System.Security.Claims.ClaimsPrincipal? User => accessor.HttpContext?.User;

    public string? Client
    {
        get
        {
            var value = accessor.HttpContext?.Request.Headers["X-Client"].ToString();
            return value is not null && KnownClients.Contains(value) ? value : null;
        }
    }

    public bool IsAuthenticated => (User?.Identity?.IsAuthenticated ?? false) && User?.FindFirst("userId") is not null;

    public long? UserId => GetLong("userId");
    public long? BusinessId => GetLong("businessId");
    public long? DefaultBranchId => GetLong("defaultBranchId");
    public string? DeviceId => User?.FindFirst("deviceId")?.Value ?? Header("X-Device-Id", 64);
    public string? DeviceName => Header("X-Device-Name", 200);
    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
    public string? UserAgent => Header("User-Agent", 500);
    public string? CorrelationId => accessor.HttpContext?.TraceIdentifier;

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

    private string? Header(string name, int maxLength)
    {
        var value = accessor.HttpContext?.Request.Headers[name].ToString().Trim();
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
