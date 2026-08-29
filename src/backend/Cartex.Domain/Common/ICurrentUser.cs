namespace Cartex.Domain.Common;

public interface ICurrentUser
{
    long? UserId { get; }
    long? BusinessId { get; }
    long? DefaultBranchId { get; }
    IReadOnlyCollection<long> BranchIds { get; }
    bool CanAccessAllBranches { get; }
    bool IsAuthenticated { get; }
    string? Client { get; }
    string? DeviceId { get; }
    string? DeviceName { get; }
    string? IpAddress { get; }
    string? UserAgent { get; }
    string? CorrelationId { get; }
    bool HasPermission(string permission);
}

public sealed class NullCurrentUser : ICurrentUser
{
    public long? UserId => null;
    public long? BusinessId => null;
    public long? DefaultBranchId => null;
    public IReadOnlyCollection<long> BranchIds => [];
    public bool CanAccessAllBranches => false;
    public bool IsAuthenticated => false;
    public string? Client => null;
    public string? DeviceId => null;
    public string? DeviceName => null;
    public string? IpAddress => null;
    public string? UserAgent => null;
    public string? CorrelationId => null;
    public bool HasPermission(string permission) => false;
}
