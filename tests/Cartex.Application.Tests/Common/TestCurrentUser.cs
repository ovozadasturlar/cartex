using Cartex.Domain.Common;

namespace Cartex.Application.Tests.Common;

public sealed class TestCurrentUser : ICurrentUser
{
    public bool IsAuthenticated { get; private set; }
    public long? UserId { get; private set; }
    public long? BusinessId { get; private set; }
    public long? DefaultBranchId { get; private set; }
    public IReadOnlyCollection<long> BranchIds { get; private set; } = [];
    public bool CanAccessAllBranches { get; private set; }

    public void Reset()
    {
        IsAuthenticated = false;
        UserId = null;
        BusinessId = null;
        DefaultBranchId = null;
        BranchIds = [];
        CanAccessAllBranches = false;
    }

    public void AsAdmin(long userId, long businessId, params long[] branches)
    {
        IsAuthenticated = true;
        UserId = userId;
        BusinessId = businessId;
        BranchIds = branches;
        DefaultBranchId = branches.Length > 0 ? branches[0] : null;
        CanAccessAllBranches = true;
    }

    public void AsCashier(long userId, long businessId, long branch)
    {
        IsAuthenticated = true;
        UserId = userId;
        BusinessId = businessId;
        BranchIds = [branch];
        DefaultBranchId = branch;
        CanAccessAllBranches = false;
    }
}
