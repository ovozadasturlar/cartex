namespace Cartex.Shared.Models.Warehouses;

public record WarehouseDto(long Id, string Name, long BranchId, string BranchName, bool IsOnline = false);
