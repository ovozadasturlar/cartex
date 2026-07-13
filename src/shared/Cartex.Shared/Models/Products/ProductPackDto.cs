namespace Cartex.Shared.Models.Products;

// Hajm mahsulotning saqlash birligida: "Qop" = 50 (kg), "Quti" = 12 (dona).
public record ProductPackDto(long Id, long ProductId, string Name, decimal Size, string Kind, bool IsDefault);

public record SaveProductPackRequest(string Name, decimal Size, string Kind, bool IsDefault);
