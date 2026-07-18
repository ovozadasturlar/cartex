using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Suppliers.Queries;

public record GetSupplierLedgerQuery(long SupplierId, int Page = 1, int PageSize = 50) : IRequest<IReadOnlyCollection<SupplierLedgerEntryDto>>;

public record SupplierLedgerEntryDto(DateTime Date, string OperationType, string AccountType, decimal Change, decimal BalanceAfter, string? Currency = null);

public sealed class GetSupplierLedgerQueryHandler(
    IApplicationDbContext db,
    IPagingMetadataWriter writer) : IRequestHandler<GetSupplierLedgerQuery, IReadOnlyCollection<SupplierLedgerEntryDto>>
{
    public async Task<IReadOnlyCollection<SupplierLedgerEntryDto>> Handle(GetSupplierLedgerQuery request, CancellationToken cancellationToken)
    {
        var accounts = await db.Accounts
            .Where(a => a.SupplierId == request.SupplierId)
            .Select(a => new { a.Id, a.Type, a.Currency })
            .ToListAsync(cancellationToken);

        if (accounts.Count == 0)
            return [];

        var baseCode = await db.Businesses.Select(b => b.Currency).FirstAsync(cancellationToken);
        var typeById = accounts.ToDictionary(a => a.Id, a => a.Type);
        var currencyById = accounts.ToDictionary(a => a.Id, a => a.Currency == baseCode ? null : a.Currency);
        var ids = typeById.Keys.ToList();

        var txQuery = db.Transactions
            .Where(t => (t.FromAccountId != null && ids.Contains(t.FromAccountId.Value))
                     || (t.ToAccountId != null && ids.Contains(t.ToAccountId.Value)));

        if (request.Page <= 0 || request.PageSize <= 0)
        {
            var all = await txQuery
                .OrderBy(t => t.CreatedAt).ThenBy(t => t.Id)
                .Select(t => new TxRow(t.Id, t.CreatedAt, t.OperationType, t.Amount, t.FromAccountId, t.ToAccountId))
                .ToListAsync(cancellationToken);
            var full = BuildEntries(all, typeById, currencyById, ids.ToDictionary(id => id, _ => 0m));
            full.Reverse();
            return full;
        }

        var total = await txQuery.CountAsync(cancellationToken);
        writer.Write(new PagedListMetadata(total, request.Page, request.PageSize,
            (int)Math.Ceiling(total / (double)request.PageSize)));

        var pageTx = await txQuery
            .OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(t => new TxRow(t.Id, t.CreatedAt, t.OperationType, t.Amount, t.FromAccountId, t.ToAccountId))
            .ToListAsync(cancellationToken);

        if (pageTx.Count == 0)
            return [];

        var oldest = pageTx[^1];
        var prior = txQuery.Where(t => t.CreatedAt < oldest.CreatedAt
            || (t.CreatedAt == oldest.CreatedAt && t.Id < oldest.Id));
        var priorSums = await prior
            .Where(t => t.ToAccountId != null && ids.Contains(t.ToAccountId.Value))
            .Select(t => new { Id = t.ToAccountId!.Value, Amount = -t.Amount })
            .Concat(prior
                .Where(t => t.FromAccountId != null && ids.Contains(t.FromAccountId.Value))
                .Select(t => new { Id = t.FromAccountId!.Value, Amount = t.Amount }))
            .GroupBy(x => x.Id)
            .Select(g => new { Id = g.Key, Sum = g.Sum(x => x.Amount) })
            .ToListAsync(cancellationToken);

        var running = ids.ToDictionary(id => id, _ => 0m);
        foreach (var x in priorSums) running[x.Id] += x.Sum;

        pageTx.Reverse();
        var entries = BuildEntries(pageTx, typeById, currencyById, running);
        entries.Reverse();
        return entries;
    }

    private sealed record TxRow(long Id, DateTime CreatedAt, OperationType OperationType, decimal Amount, long? FromAccountId, long? ToAccountId);

    private static List<SupplierLedgerEntryDto> BuildEntries(
        List<TxRow> transactions,
        Dictionary<long, AccountType> typeById,
        Dictionary<long, string?> currencyById,
        Dictionary<long, decimal> running)
    {
        var entries = new List<SupplierLedgerEntryDto>();
        foreach (var t in transactions)
        {
            if (t.FromAccountId is long from && typeById.TryGetValue(from, out var fromType))
            {
                var balance = running.GetValueOrDefault(from) + t.Amount;
                running[from] = balance;
                entries.Add(new SupplierLedgerEntryDto(t.CreatedAt, t.OperationType.ToString(), fromType.ToString(), t.Amount, balance, currencyById.GetValueOrDefault(from)));
            }

            if (t.ToAccountId is long to && typeById.TryGetValue(to, out var toType))
            {
                var balance = running.GetValueOrDefault(to) - t.Amount;
                running[to] = balance;
                entries.Add(new SupplierLedgerEntryDto(t.CreatedAt, t.OperationType.ToString(), toType.ToString(), -t.Amount, balance, currencyById.GetValueOrDefault(to)));
            }
        }
        return entries;
    }
}
