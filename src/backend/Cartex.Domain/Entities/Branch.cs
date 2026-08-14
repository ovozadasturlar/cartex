using Cartex.Domain.Common;

namespace Cartex.Domain.Entities;

public class Branch : SoftDeleteEntity
{
    public long BusinessId { get; set; }
    public Business Business { get; set; } = null!;

    public string Name { get; set; } = null!;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;
    public bool AutoTrustPrintDevices { get; set; }

    public ICollection<Warehouse> Warehouses { get; set; } = [];
    public ICollection<UserBranch> UserBranches { get; set; } = [];
    public ICollection<BranchCatalogEntry> CatalogEntries { get; set; } = [];
}
