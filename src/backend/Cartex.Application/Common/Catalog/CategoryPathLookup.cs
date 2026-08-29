using Cartex.Domain.Common;
using Cartex.Persistence;
using Cartex.Shared.Models.Categories;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Common.Catalog;

public static class CategoryPathLookup
{
    public static async Task<IReadOnlyDictionary<long, string>> LoadAsync(
        IApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        var rows = await db.Categories.AsNoTracking()
            .Select(x => new Row(x.Id, x.Name, x.ParentId))
            .ToListAsync(cancellationToken);
        var byId = rows.ToDictionary(x => x.Id);
        var paths = new Dictionary<long, string>();

        string Build(Row row, HashSet<long> visited)
        {
            if (paths.TryGetValue(row.Id, out var existing)) return existing;
            if (!visited.Add(row.Id))
                throw new BusinessRuleException("Kategoriya daraxtida sikl bor.", "category_cycle");
            var path = row.ParentId is { } parentId && byId.TryGetValue(parentId, out var parent)
                ? CategoryPath.Build(Build(parent, visited), row.Name)
                : row.Name;
            visited.Remove(row.Id);
            paths[row.Id] = path;
            return path;
        }

        foreach (var row in rows)
            Build(row, []);
        return paths;
    }

    private sealed record Row(long Id, string Name, long? ParentId);
}
