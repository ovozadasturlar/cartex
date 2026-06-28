namespace Cartex.Shared.Models.Branches;

public record BranchDto(long Id, string Name, string? Address, string? Phone, bool IsActive);
