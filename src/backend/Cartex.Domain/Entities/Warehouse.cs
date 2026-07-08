using Cartex.Domain.Common;

namespace Cartex.Domain.Entities;

public class Warehouse : SoftDeleteEntity, IBranchScoped
{
    public long BranchId { get; set; }
    public Branch Branch { get; set; } = null!;

    public string Name { get; set; } = null!;
    public bool IsOnline { get; set; }
    public long? AssignedUserId { get; set; }
    public User? AssignedUser { get; set; }

    public ICollection<Stock> Stocks { get; set; } = [];
}
