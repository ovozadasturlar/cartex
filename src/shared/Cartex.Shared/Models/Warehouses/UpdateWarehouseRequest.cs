namespace Cartex.Shared.Models.Warehouses;

public record UpdateWarehouseRequest(string Name, bool IsOnline = false);
