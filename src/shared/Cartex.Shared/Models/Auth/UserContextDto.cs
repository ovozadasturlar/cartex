namespace Cartex.Shared.Models.Auth;

public record UserContextBranchDto(long Id, string Name, bool IsActive);
public record UserContextWarehouseDto(long Id, string Name, long BranchId, string BranchName);
public record UserContextDto(
    long? DefaultBranchId,
    List<UserContextBranchDto> Branches,
    List<UserContextWarehouseDto> Warehouses);
