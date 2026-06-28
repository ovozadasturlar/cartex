namespace Cartex.Shared.Models.Branches;

public record UpdateBranchRequest(string Name, string? Address, string? Phone, bool IsActive);
