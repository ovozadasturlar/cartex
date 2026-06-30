namespace Cartex.Shared.Models.Categories;

public record CreateCategoryRequest(string Name, long? ParentId, string? Description = null);
