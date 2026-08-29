namespace Cartex.Shared.Models.Suppliers;

public record UpdateSupplierRequest(string Name, string? Phone, bool AcceptsReturns = false);
