namespace Cartex.Application.Common.Extensions;

using Cartex.Application.Common.Models;

public static class SortingExtensions
{
    public static IQueryable<T> AsSortable<T>(this IQueryable<T> query, SortingRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.SortBy) && !QueryExtensions.IsSensitive(request.SortBy))
        {
            var exists = QueryExtensions.GetCachedProperties(typeof(T))
                .Any(p => string.Equals(p.Name, request.SortBy, StringComparison.OrdinalIgnoreCase));
            if (exists)
                return request.Descending
                    ? query.OrderByDescendingDynamic(request.SortBy)
                    : query.OrderByDynamic(request.SortBy);
        }
        // A'zoga to'g'ridan-to'g'ri murojaat: `EF.Property` faqat entity ustida ishlaydi, ro'yxat
        // so'rovi esa filtrni yassi proyeksiyaga qo'llashi mumkin (navigatsiya ortidagi maydonlar).
        return query
            .OrderByDescendingDynamic("CreatedAt")
            .ThenByDescendingDynamic("Id");
    }
}
