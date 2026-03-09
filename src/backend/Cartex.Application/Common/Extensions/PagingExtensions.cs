namespace Cartex.Application.Common.Extensions;

using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

public static class PagingExtensions
{
    public static async Task<IReadOnlyCollection<TResult>> ToPagedListAsync<TEntity, TResult>(
        this IQueryable<TEntity> query,
        FilteringRequest request,
        Expression<Func<TEntity, TResult>> selector,
        IPagingMetadataWriter? writer = null,
        CancellationToken cancellationToken = default) where TEntity : class
    {
        var filtered = query.AsFilterable(request);
        var total = await filtered.CountAsync(cancellationToken);

        if (request.Page <= 0 || request.PageSize <= 0)
            return await filtered.Select(selector).ToListAsync(cancellationToken);

        var items = await filtered
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(selector)
            .ToListAsync(cancellationToken);

        writer?.Write(new PagedListMetadata(total, request.Page, request.PageSize,
            (int)Math.Ceiling((double)total / request.PageSize)));

        return items;
    }
}
