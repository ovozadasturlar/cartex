namespace Cartex.Shared.Models.Branches;

public record BranchCatalogItemDto(long VariantId, string ProductName, string? Code, string? Barcode, bool IsActive, string VisibilityOverride);

public record BranchCatalogPageDto(IReadOnlyCollection<BranchCatalogItemDto> Items, int TotalCount);

public record SetBranchCatalogVisibilityRequest(string VisibilityOverride);
