using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Application.Common.Messaging;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Customers.Queries;

public record GetCustomerLedgerQuery(long CustomerId, int Page = 1, int PageSize = 50) : IRequest<IReadOnlyCollection<CustomerLedgerEntryDto>>;

public record CustomerLedgerEntryDto(DateTime Date, string OperationType, string AccountType, decimal Change, decimal BalanceAfter);

public sealed class GetCustomerLedgerQueryHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IPagingMetadataWriter writer) : IRequestHandler<GetCustomerLedgerQuery, IReadOnlyCollection<CustomerLedgerEntryDto>>
{
    public async Task<IReadOnlyCollection<CustomerLedgerEntryDto>> Handle(GetCustomerLedgerQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(AppPermissions.Customers.ViewAll) &&
            !await db.Customers.AnyAsync(c => c.Id == request.CustomerId && c.AgentId == currentUser.UserId, cancellationToken))
            return [];

        var accounts = await db.Accounts
            .Where(a => a.CustomerId == request.CustomerId)
            .Select(a => new { a.Id, a.Type })
            .ToListAsync(cancellationToken);

        if (accounts.Count == 0)
            return [];

        var typeById = accounts.ToDictionary(a => a.Id, a => a.Type);
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
            var full = BuildEntries(all, typeById, ids.ToDictionary(id => id, _ => 0m));
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
            .Select(t => new { Id = t.ToAccountId!.Value, Amount = t.Amount })
            .Concat(prior
                .Where(t => t.FromAccountId != null && ids.Contains(t.FromAccountId.Value))
                .Select(t => new { Id = t.FromAccountId!.Value, Amount = -t.Amount }))
            .GroupBy(x => x.Id)
            .Select(g => new { Id = g.Key, Sum = g.Sum(x => x.Amount) })
            .ToListAsync(cancellationToken);

        var running = ids.ToDictionary(id => id, _ => 0m);
        foreach (var x in priorSums) running[x.Id] += x.Sum;

        pageTx.Reverse();
        var entries = BuildEntries(pageTx, typeById, running);
        entries.Reverse();
        return entries;
    }

    private sealed record TxRow(long Id, DateTime CreatedAt, OperationType OperationType, decimal Amount, long? FromAccountId, long? ToAccountId);

    private static List<CustomerLedgerEntryDto> BuildEntries(
        List<TxRow> transactions,
        Dictionary<long, AccountType> typeById,
        Dictionary<long, decimal> running)
    {
        var entries = new List<CustomerLedgerEntryDto>();
        foreach (var t in transactions)
        {
            if (t.FromAccountId is long from && typeById.TryGetValue(from, out var fromType))
            {
                var balance = running.GetValueOrDefault(from) - t.Amount;
                running[from] = balance;
                entries.Add(new CustomerLedgerEntryDto(t.CreatedAt, t.OperationType.ToString(), fromType.ToString(), -t.Amount, balance));
            }

            if (t.ToAccountId is long to && typeById.TryGetValue(to, out var toType))
            {
                var balance = running.GetValueOrDefault(to) + t.Amount;
                running[to] = balance;
                entries.Add(new CustomerLedgerEntryDto(t.CreatedAt, t.OperationType.ToString(), toType.ToString(), t.Amount, balance));
            }
        }
        return entries;
    }
}
