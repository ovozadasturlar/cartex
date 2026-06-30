namespace Cartex.Shared.Models.Categories;

public record CategoryDto(long Id, string Name, string? Description, long? ParentId, string? ParentName);
