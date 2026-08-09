using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Sales.Queries;

internal static class SaleQueryFilters
{
    public static IQueryable<Sale> ApplySaleScope(
        this IQueryable<Sale> query,
        FilteringRequest request,
        ICurrentUser currentUser,
        DateTime? fromDate,
        DateTime? toDate,
        long? warehouseId,
        long? customerId)
    {
        if (!currentUser.HasPermission(AppPermissions.Sales.ViewAll))
            query = query.Where(s => s.UserId == currentUser.UserId);

        if (fromDate is { } from)
            query = query.Where(s => s.CreatedAt >= AsUtc(from));
        if (toDate is { } to)
            query = query.Where(s => s.CreatedAt < AsUtc(to));
        if (warehouseId is { } warehouse)
            query = query.Where(s => s.WarehouseId == warehouse);
        if (customerId is { } customer)
            query = query.Where(s => s.CustomerId == customer);

        foreach (var token in SearchTokens(request.Search))
        {
            var pattern = $"%{EscapeLike(token)}%";
            var isId = long.TryParse(token, out var id);
            query = query.Where(s =>
                (isId && s.Id == id) ||
                EF.Functions.ILike(s.ReceiptToken, pattern, "\\") ||
                (s.Customer != null && EF.Functions.ILike(s.Customer.FullName, pattern, "\\")) ||
                EF.Functions.ILike(s.User.FullName, pattern, "\\") ||
                s.Items.Any(i => EF.Functions.ILike(i.Variant.Product.Name, pattern, "\\")));
        }

        return query;
    }

    public static T WithoutSearch<T>(this T request) where T : FilteringRequest
    {
        request.Search = null;
        return request;
    }

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static IEnumerable<string> SearchTokens(string? search) =>
        string.IsNullOrWhiteSpace(search)
            ? []
            : search.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Take(6);

    private static string EscapeLike(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal);
}
