using Cartex.Domain.Common;
using Cartex.Domain.Entities;

namespace Cartex.Application.Categories;

internal static class CategoryTreeRules
{
    private const int MaxDepth = 3;

    public static void ValidateParent(IReadOnlyCollection<Category> categories, Category? source, long? parentId)
    {
        var byId = categories.ToDictionary(x => x.Id);
        if (parentId is { } id && !byId.ContainsKey(id))
            throw new NotFoundException("Ota kategoriya topilmadi.", "category_parent_not_found");

        var visited = new HashSet<long>();
        var cursor = parentId;
        while (cursor is { } current)
        {
            if (!visited.Add(current) || source?.Id == current)
                throw new BusinessRuleException("Kategoriyani o'z avlodiga ko'chirib bo'lmaydi.", "category_cycle");
            cursor = byId[current].ParentId;
        }

        var depth = parentId is null ? 1 : Depth(byId[parentId.Value], byId) + 1;
        var height = source is null ? 1 : Height(source, categories, []);
        if (depth + height - 1 > MaxDepth)
            throw new BusinessRuleException("Kategoriya daraxti 3 darajadan chuqur bo'lmaydi.", "category_depth_exceeded");
    }

    public static void Move(IReadOnlyCollection<Category> categories, Category source, long? parentId, int sortOrder)
    {
        ValidateParent(categories, source, parentId);
        var oldParentId = source.ParentId;
        if (oldParentId != parentId)
            Normalize(Ordered(categories.Where(x => x.Id != source.Id && x.ParentId == oldParentId)));

        source.ParentId = parentId;
        var siblings = Ordered(categories.Where(x => x.Id != source.Id && x.ParentId == parentId)).ToList();
        siblings.Insert(Math.Clamp(sortOrder, 0, siblings.Count), source);
        Normalize(siblings);
    }

    /// O'qish so'rovi bilan aynan bir xil tartib. Farq qilsa, `SortOrder` hali teng
    /// bo'lgan holatda (masalan migratsiyadan keyin) birinchi ko'chirish butun
    /// ro'yxatni ko'rinib turganidan boshqa tartibga o'tkazib yuboradi.
    private static IEnumerable<Category> Ordered(IEnumerable<Category> categories) => categories
        .OrderBy(x => x.SortOrder)
        .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
        .ThenBy(x => x.Id);

    public static void Normalize(IEnumerable<Category> categories)
    {
        var index = 0;
        foreach (var category in categories)
            category.SortOrder = index++;
    }

    public static bool IsDescendant(IReadOnlyCollection<Category> categories, long sourceId, long candidateId)
    {
        var byId = categories.ToDictionary(x => x.Id);
        var cursor = (long?)candidateId;
        var visited = new HashSet<long>();
        while (cursor is { } current && visited.Add(current))
        {
            if (current == sourceId) return true;
            cursor = byId.GetValueOrDefault(current)?.ParentId;
        }
        return false;
    }

    private static int Depth(Category category, IReadOnlyDictionary<long, Category> byId)
    {
        var depth = 1;
        var cursor = category;
        var visited = new HashSet<long> { category.Id };
        while (cursor.ParentId is { } parentId)
        {
            if (!visited.Add(parentId))
                throw new BusinessRuleException("Kategoriya daraxtida sikl bor.", "category_cycle");
            cursor = byId[parentId];
            depth++;
        }
        return depth;
    }

    private static int Height(Category category, IReadOnlyCollection<Category> categories, HashSet<long> path)
    {
        if (!path.Add(category.Id))
            throw new BusinessRuleException("Kategoriya daraxtida sikl bor.", "category_cycle");
        var children = categories.Where(x => x.ParentId == category.Id).ToList();
        var height = children.Count == 0 ? 1 : 1 + children.Max(x => Height(x, categories, path));
        path.Remove(category.Id);
        return height;
    }
}
