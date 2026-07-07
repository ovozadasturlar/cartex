using Cartex.Application.Common.Messaging;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Reports.Queries;

public record DailyCashFlowDto(DateTime Date, decimal Income, decimal Expense, decimal Sales);

public record GetCashFlowQuery(DateTime From, DateTime To) : IRequest<IReadOnlyCollection<DailyCashFlowDto>>;

public sealed class GetCashFlowQueryHandler(IApplicationDbContext db) : IRequestHandler<GetCashFlowQuery, IReadOnlyCollection<DailyCashFlowDto>>
{
    public async Task<IReadOnlyCollection<DailyCashFlowDto>> Handle(GetCashFlowQuery request, CancellationToken cancellationToken)
    {
        var from = DateTime.SpecifyKind(request.From, DateTimeKind.Utc);
        var to = DateTime.SpecifyKind(request.To, DateTimeKind.Utc);

        var rows = await db.Transactions
            .Where(t => t.CreatedAt >= from && t.CreatedAt < to)
            .Select(t => new
            {
                t.CreatedAt,
                Base = t.Amount * t.Rate,
                Incoming = t.ToAccount != null && t.ToAccount.BranchId != null,
                Outgoing = t.FromAccount != null && t.FromAccount.BranchId != null
            })
            .ToListAsync(cancellationToken);

        var sales = await db.Sales
            .Where(s => s.CreatedAt >= from && s.CreatedAt < to)
            .Select(s => new { s.CreatedAt, s.TotalAmount })
            .ToListAsync(cancellationToken);

        return Enumerable.Range(0, Math.Max(1, (int)(to - from).TotalDays))
            .Select(offset =>
            {
                var day = from.AddDays(offset).Date;
                var daily = rows.Where(r => r.CreatedAt.Date == day).ToList();
                return new DailyCashFlowDto(day,
                    daily.Where(r => r.Incoming && !r.Outgoing).Sum(r => r.Base),
                    daily.Where(r => r.Outgoing && !r.Incoming).Sum(r => r.Base),
                    sales.Where(s => s.CreatedAt.Date == day).Sum(s => s.TotalAmount));
            })
            .ToList();
    }
}
