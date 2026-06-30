namespace Cartex.Application.Common.Extensions;

using Cartex.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

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
        return query.OrderBy(x => EF.Property<long>(x!, "Id"));
    }
}
