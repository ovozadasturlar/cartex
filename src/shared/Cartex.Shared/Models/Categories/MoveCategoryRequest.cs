namespace Cartex.Shared.Models.Categories;

public record MoveCategoryRequest(long? ParentId, int SortOrder);
