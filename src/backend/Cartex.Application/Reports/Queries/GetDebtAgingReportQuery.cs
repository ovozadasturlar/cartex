using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Reports.Queries;

public record GetDebtAgingReportQuery : IRequest<DebtAgingReportDto>;

public record DebtAgingRowDto(long CustomerId, string CustomerName, decimal Balance, string Currency, decimal BalanceBase, DateTime? LastActivity, int DaysOverdue, string Bucket);

public record DebtAgingReportDto(decimal Total, decimal Bucket0_30, decimal Bucket31_60, decimal Bucket60Plus, List<DebtAgingRowDto> Rows);

public sealed class GetDebtAgingReportQueryHandler(IApplicationDbContext db) : IRequestHandler<GetDebtAgingReportQuery, DebtAgingReportDto>
{
    public async Task<DebtAgingReportDto> Handle(GetDebtAgingReportQuery request, CancellationToken cancellationToken)
    {
        var baseCode = await db.Businesses.Select(b => b.Currency).FirstAsync(cancellationToken);
        var accounts = await db.Accounts
            .Where(a => a.Type == AccountType.Debt && a.CustomerId != null && a.Balance > 0)
            .Select(a => new
            {
                a.Id,
                CustomerId = a.CustomerId!.Value,
                CustomerName = a.Customer!.FullName,
                a.Balance,
                a.Currency,
                BalanceBase = a.Balance * (a.Currency == baseCode ? 1m
                    : db.ExchangeRates.Where(r => r.Code == a.Currency).OrderByDescending(r => r.EffectiveAt).Select(r => r.Rate).FirstOrDefault())
            })
            .ToListAsync(cancellationToken);

        var accountIds = accounts.Select(a => a.Id).ToList();
        var activity = await db.Transactions
            .Where(t => t.OperationType == OperationType.DebtCharge || t.OperationType == OperationType.DebtPay)
            .Where(t => (t.FromAccountId != null && accountIds.Contains(t.FromAccountId.Value))
                     || (t.ToAccountId != null && accountIds.Contains(t.ToAccountId.Value)))
            .Select(t => new { t.FromAccountId, t.ToAccountId, t.CreatedAt })
            .ToListAsync(cancellationToken);

        var lastByAccount = new Dictionary<long, DateTime>();
        foreach (var t in activity)
            foreach (var id in new[] { t.FromAccountId, t.ToAccountId })
                if (id is { } aid && accountIds.Contains(aid) && (!lastByAccount.TryGetValue(aid, out var cur) || t.CreatedAt > cur))
                    lastByAccount[aid] = t.CreatedAt;

        var now = DateTime.UtcNow;
        var rows = accounts
            .Select(a =>
            {
                var hasLast = lastByAccount.TryGetValue(a.Id, out var last);
                var days = hasLast ? (int)(now - last).TotalDays : 0;
                var bucket = days <= 30 ? "0-30" : days <= 60 ? "31-60" : "60+";
                return new DebtAgingRowDto(a.CustomerId, a.CustomerName, a.Balance, a.Currency, a.BalanceBase, hasLast ? last : null, days, bucket);
            })
            .OrderByDescending(r => r.BalanceBase)
            .ToList();

        return new DebtAgingReportDto(
            rows.Sum(r => r.BalanceBase),
            rows.Where(r => r.Bucket == "0-30").Sum(r => r.BalanceBase),
            rows.Where(r => r.Bucket == "31-60").Sum(r => r.BalanceBase),
            rows.Where(r => r.Bucket == "60+").Sum(r => r.BalanceBase),
            rows);
    }
}
