using Cartex.Domain.Common;

namespace Cartex.Domain.Entities;

public class Shop : BaseEntity
{
    public string Name { get; set; } = null!;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public decimal CashbackRate { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Warehouse> Warehouses { get; set; } = [];
    public ICollection<User> Users { get; set; } = [];
}
