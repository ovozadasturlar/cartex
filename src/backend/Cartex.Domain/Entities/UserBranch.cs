namespace Cartex.Domain.Entities;

public class UserBranch
{
    public long UserId { get; set; }
    public User User { get; set; } = null!;

    public long BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
}
