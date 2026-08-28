namespace Cartex.Shared.Models.Suppliers;

public record CreateSupplierRequest(string Name, string? Phone, bool AcceptsReturns = false);
