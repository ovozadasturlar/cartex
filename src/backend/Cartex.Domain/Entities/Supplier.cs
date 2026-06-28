using Cartex.Domain.Common;

namespace Cartex.Domain.Entities;

public class Supplier : SoftDeleteEntity
{
    public string Name { get; set; } = null!;
    public string? Phone { get; set; }

    public ICollection<Supply> Supplies { get; set; } = [];
    public ICollection<Account> Accounts { get; set; } = [];
}
