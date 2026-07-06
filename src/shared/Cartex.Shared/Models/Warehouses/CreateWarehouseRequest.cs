namespace Cartex.Shared.Models.Warehouses;

public record CreateWarehouseRequest(long BranchId, string Name, bool IsOnline = false);
