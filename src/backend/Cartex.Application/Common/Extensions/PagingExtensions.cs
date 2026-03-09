namespace Cartex.Application.Common.Extensions;

using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

public static class PagingExtensions
{
    public static async Task<IReadOnlyCollection<T>> ToPagedListAsync<T>(
        this IQueryable<T> query,
        FilteringRequest request,
        IPagingMetadataWriter? writer = null,
        CancellationToken cancellationToken = default) where T : class
    {
        var filtered = query.AsFilterable(request);
        var total = await filtered.CountAsync(cancellationToken);

        if (request.Page <= 0 || request.PageSize <= 0)
            return await filtered.ToListAsync(cancellationToken);

        var items = await filtered
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        writer?.Write(new PagedListMetadata(total, request.Page, request.PageSize,
            (int)Math.Ceiling((double)total / request.PageSize)));

        return items;
    }
}
