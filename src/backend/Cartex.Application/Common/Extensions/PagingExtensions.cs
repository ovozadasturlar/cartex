namespace Cartex.Application.Common.Extensions;

using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using Cartex.Shared.Models.Common;

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
        {
            if (total > PagingRequest.MaxUnboundedSize)
                throw new BusinessRuleException($"Natija juda katta ({total}). Iltimos, filtr yoki sahifalashdan foydalaning.");
            return await filtered.Select(selector).ToListAsync(cancellationToken);
        }

        var page = request.Page;
        var pageSize = Math.Min(request.PageSize, PagingRequest.MaxPageSize);

        var items = await filtered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(selector)
            .ToListAsync(cancellationToken);

        writer?.Write(new PagedListMetadata(total, page, pageSize,
            (int)Math.Ceiling((double)total / pageSize)));

        return items;
    }
}
