namespace Cartex.Shared.Models.Categories;

public record UpdateCategoryRequest(string Name, long? ParentId, string? Description = null);
