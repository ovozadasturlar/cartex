using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Persistence;
using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Customers.Queries;

public record GetCustomerLedgerQuery(long CustomerId, int Page = 1, int PageSize = 50) : IRequest<IReadOnlyCollection<CustomerLedgerEntryDto>>;

public record CustomerLedgerEntryDto(DateTime Date, string OperationType, string AccountType, decimal Change, decimal BalanceAfter);

public sealed class GetCustomerLedgerQueryHandler(
    IApplicationDbContext db,
    IPagingMetadataWriter writer) : IRequestHandler<GetCustomerLedgerQuery, IReadOnlyCollection<CustomerLedgerEntryDto>>
{
    public async Task<IReadOnlyCollection<CustomerLedgerEntryDto>> Handle(GetCustomerLedgerQuery request, CancellationToken cancellationToken)
    {
        var accounts = await db.Accounts
            .Where(a => a.CustomerId == request.CustomerId)
            .Select(a => new { a.Id, a.Type })
            .ToListAsync(cancellationToken);

        if (accounts.Count == 0)
            return [];

        var typeById = accounts.ToDictionary(a => a.Id, a => a.Type);
        var ids = typeById.Keys.ToList();

        var transactions = await db.Transactions
            .Where(t => (t.FromAccountId != null && ids.Contains(t.FromAccountId.Value))
                     || (t.ToAccountId != null && ids.Contains(t.ToAccountId.Value)))
            .OrderBy(t => t.CreatedAt).ThenBy(t => t.Id)
            .Select(t => new { t.CreatedAt, t.OperationType, t.Amount, t.FromAccountId, t.ToAccountId })
            .ToListAsync(cancellationToken);

        var running = new Dictionary<long, decimal>();
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

        entries.Reverse();

        if (request.Page <= 0 || request.PageSize <= 0)
            return entries;

        var total = entries.Count;
        var totalPages = (int)Math.Ceiling(total / (double)request.PageSize);
        writer.Write(new PagedListMetadata(total, request.Page, request.PageSize, totalPages));

        return entries
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();
    }
}
