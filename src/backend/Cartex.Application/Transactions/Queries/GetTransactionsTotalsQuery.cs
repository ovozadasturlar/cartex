using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Models;
using Cartex.Domain.Common;
using Cartex.Persistence;
using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Transactions.Queries;

public record GetTransactionsTotalsQuery : FilteringRequest, IRequest<TransactionsTotalsDto>
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? OperationType { get; set; }
}

public record TransactionsTotalsDto(int Count, decimal TotalAmount);

public sealed class GetTransactionsTotalsQueryHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser) : IRequestHandler<GetTransactionsTotalsQuery, TransactionsTotalsDto>
{
    public async Task<TransactionsTotalsDto> Handle(GetTransactionsTotalsQuery request, CancellationToken cancellationToken)
    {
        var query = db.Transactions.AsFilterable(request);
        if (!currentUser.CanAccessAllBranches)
            query = query.Where(t => t.BranchId == null || currentUser.BranchIds.Contains(t.BranchId.Value));

        if (request.FromDate is { } fromDate)
            query = query.Where(t => t.CreatedAt >= DateTime.SpecifyKind(fromDate, DateTimeKind.Utc));
        if (request.ToDate is { } toDate)
            query = query.Where(t => t.CreatedAt < DateTime.SpecifyKind(toDate, DateTimeKind.Utc));
        if (!string.IsNullOrEmpty(request.OperationType) && Enum.TryParse<Cartex.Domain.Enums.OperationType>(request.OperationType, out var ot))
            query = query.Where(t => t.OperationType == ot);

        var totals = await query
            .GroupBy(_ => 1)
            .Select(g => new { Count = g.Count(), Total = g.Sum(t => t.Amount) })
            .FirstOrDefaultAsync(cancellationToken);
        return totals is null
            ? new TransactionsTotalsDto(0, 0)
            : new TransactionsTotalsDto(totals.Count, totals.Total);
    }
}
