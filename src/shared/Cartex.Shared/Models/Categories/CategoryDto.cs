namespace Cartex.Shared.Models.Categories;

public record CategoryDto(long Id, string Name, long? ParentId, string? ParentName);
