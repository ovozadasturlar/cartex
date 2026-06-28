using Cartex.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Customers.Queries;

public record GetCustomerLedgerQuery(long CustomerId) : IRequest<IReadOnlyCollection<CustomerLedgerEntryDto>>;

public record CustomerLedgerEntryDto(DateTime Date, string OperationType, string AccountType, decimal Change, decimal BalanceAfter);

public sealed class GetCustomerLedgerQueryHandler(IApplicationDbContext db) : IRequestHandler<GetCustomerLedgerQuery, IReadOnlyCollection<CustomerLedgerEntryDto>>
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

        return entries;
    }
}
