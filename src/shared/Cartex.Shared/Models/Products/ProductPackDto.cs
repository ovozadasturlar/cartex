namespace Cartex.Shared.Models.Products;

public record ProductPackDto(long Id, long ProductId, string Name, decimal Size, string Kind, bool IsDefault);

public record SaveProductPackRequest(string Name, decimal Size, string Kind, bool IsDefault);
