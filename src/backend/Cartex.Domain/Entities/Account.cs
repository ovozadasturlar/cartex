using Cartex.Domain.Common;
using Cartex.Domain.Enums;

namespace Cartex.Domain.Entities;

public class Account : SoftDeleteEntity
{
    public AccountOwnerType OwnerType { get; set; }
    public long OwnerId { get; set; }
    public string Name { get; set; } = null!;
    public AccountType Type { get; set; }
    public decimal Balance { get; set; }
}
