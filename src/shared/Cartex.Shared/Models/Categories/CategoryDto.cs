namespace Cartex.Shared.Models.Categories;

public record CategoryDto(
    long Id,
    string Name,
    string? Description,
    long? ParentId,
    string? ParentName,
    int SortOrder = 0,
    string? FullPath = null,
    int DescendantProductCount = 0,
    int Depth = 0,
    bool IsMatch = false,
    int ProductCount = 0)
{
    public string TreeDisplayName => new string('\u2003', Math.Max(0, Depth - 1))
        + CategoryPath.Truncate(FullPath ?? Name, 56);
}
