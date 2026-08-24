using Cartex.Application.Common.Models;
using Cartex.Domain.Common;
using Cartex.Persistence;
using Cartex.Shared.Models.Categories;
using Cartex.Shared.Search;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Categories.Queries;

public record GetCategoriesQuery : FilteringRequest, IRequest<IReadOnlyCollection<CategoryDto>>;

public sealed class GetCategoriesQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetCategoriesQuery, IReadOnlyCollection<CategoryDto>>
{
    public async Task<IReadOnlyCollection<CategoryDto>> Handle(GetCategoriesQuery request, CancellationToken cancellationToken)
    {
        var rows = await db.Categories.AsNoTracking()
            .Select(x => new CategoryRow(x.Id, x.Name, x.SearchFold, x.Description, x.ParentId, x.SortOrder))
            .ToListAsync(cancellationToken);
        var directCounts = await db.Products.AsNoTracking()
            .Where(x => x.CategoryId != null)
            .GroupBy(x => x.CategoryId!.Value)
            .Select(x => new { Id = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, cancellationToken);
        var byId = rows.ToDictionary(x => x.Id);
        var children = rows.ToLookup(x => x.ParentId);
        var paths = new Dictionary<long, (string Path, int Depth)>();
        var counts = new Dictionary<long, int>();

        (string Path, int Depth) Path(CategoryRow row, HashSet<long> path)
        {
            if (paths.TryGetValue(row.Id, out var value)) return value;
            if (!path.Add(row.Id))
                throw new BusinessRuleException("Kategoriya daraxtida sikl bor.", "category_cycle");
            value = row.ParentId is { } parentId && byId.TryGetValue(parentId, out var parent)
                ? Append(Path(parent, path), row.Name)
                : (row.Name, 1);
            path.Remove(row.Id);
            paths[row.Id] = value;
            return value;
        }

        int ProductCount(long id, HashSet<long> path)
        {
            if (counts.TryGetValue(id, out var value)) return value;
            if (!path.Add(id))
                throw new BusinessRuleException("Kategoriya daraxtida sikl bor.", "category_cycle");
            value = directCounts.GetValueOrDefault(id) + children[id].Sum(x => ProductCount(x.Id, path));
            path.Remove(id);
            counts[id] = value;
            return value;
        }

        var isSearching = !string.IsNullOrWhiteSpace(request.Search);
        var query = SearchFold.Fuzzy(request.Search);
        var strictQuery = SearchFold.Strict(request.Search);
        var matched = isSearching
            ? rows.Where(x => Matches(Path(x, []).Path, x.SearchFold, strictQuery, query))
                .Select(x => x.Id)
                .ToHashSet()
            : [];
        var included = !isSearching
            ? rows.Select(x => x.Id).ToHashSet()
            : IncludeAncestors(matched, byId);

        var result = new List<CategoryDto>();
        void Add(long? parentId)
        {
            foreach (var row in children[parentId]
                         .Where(x => included.Contains(x.Id))
                         .OrderBy(x => x.SortOrder)
                         .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                         .ThenBy(x => x.Id))
            {
                var path = Path(row, []);
                result.Add(new CategoryDto(
                    row.Id,
                    row.Name,
                    row.Description,
                    row.ParentId,
                    row.ParentId is { } parentIdValue ? byId.GetValueOrDefault(parentIdValue)?.Name : null,
                    row.SortOrder,
                    path.Path,
                    ProductCount(row.Id, []),
                    path.Depth,
                    matched.Contains(row.Id),
                    directCounts.GetValueOrDefault(row.Id)));
                Add(row.Id);
            }
        }

        Add(null);
        foreach (var row in rows.Where(x => included.Contains(x.Id) && result.All(y => y.Id != x.Id)))
        {
            var path = Path(row, []);
            result.Add(new CategoryDto(row.Id, row.Name, row.Description, row.ParentId, null,
                row.SortOrder, path.Path, ProductCount(row.Id, []), path.Depth, matched.Contains(row.Id),
                directCounts.GetValueOrDefault(row.Id)));
        }
        return result;
    }

    private static (string Path, int Depth) Append((string Path, int Depth) parent, string name) =>
        (CategoryPath.Build(parent.Path, name), parent.Depth + 1);

    private static bool Matches(string path, string? storedFold, string strictQuery, string fuzzyQuery)
    {
        if (strictQuery.Length > 0 && SearchFold.Strict(path).Contains(strictQuery, StringComparison.Ordinal))
            return true;
        return fuzzyQuery.Length > 0
            && (SearchFold.Fuzzy(path).Contains(fuzzyQuery, StringComparison.Ordinal)
                || storedFold?.Contains(fuzzyQuery, StringComparison.Ordinal) == true);
    }

    private static HashSet<long> IncludeAncestors(HashSet<long> matched, IReadOnlyDictionary<long, CategoryRow> byId)
    {
        var included = new HashSet<long>(matched);
        foreach (var id in matched)
        {
            var cursor = byId[id].ParentId;
            while (cursor is { } parentId && included.Add(parentId))
                cursor = byId.GetValueOrDefault(parentId)?.ParentId;
        }
        return included;
    }

    private sealed record CategoryRow(
        long Id,
        string Name,
        string? SearchFold,
        string? Description,
        long? ParentId,
        int SortOrder);
}
