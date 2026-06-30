namespace Cartex.Shared.Models.Common;

public record PagedResult<T>(IReadOnlyList<T> Items, PagedListMetadata Meta)
{
    public static PagedResult<T> Single(IReadOnlyList<T> items) =>
        new(items, new PagedListMetadata(items.Count, 1, items.Count == 0 ? 1 : items.Count, 1));
}
