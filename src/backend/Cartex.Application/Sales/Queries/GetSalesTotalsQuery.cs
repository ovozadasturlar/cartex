using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Models;
using Cartex.Persistence;
using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Sales.Queries;

public record GetSalesTotalsQuery : FilteringRequest, IRequest<SalesTotalsDto>
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public long? WarehouseId { get; set; }
}

public record SalesTotalsDto(int Count, decimal TotalAmount, decimal TotalDiscount, decimal TotalDebt);

public sealed class GetSalesTotalsQueryHandler(IApplicationDbContext db) : IRequestHandler<GetSalesTotalsQuery, SalesTotalsDto>
{
    public async Task<SalesTotalsDto> Handle(GetSalesTotalsQuery request, CancellationToken cancellationToken)
    {
        var query = db.Sales.AsQueryable();

        if (request.FromDate is { } fromDate)
            query = query.Where(s => s.CreatedAt >= DateTime.SpecifyKind(fromDate, DateTimeKind.Utc));
        if (request.ToDate is { } toDate)
            query = query.Where(s => s.CreatedAt < DateTime.SpecifyKind(toDate, DateTimeKind.Utc));
        if (request.WarehouseId is { } warehouseId)
            query = query.Where(s => s.WarehouseId == warehouseId);

        query = query.AsFilterable(request);

        var totals = await query
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Count = g.Count(),
                Total = g.Sum(s => s.TotalAmount),
                Discount = g.Sum(s => s.DiscountAmount),
                Debt = g.Sum(s => s.DebtAmount)
            })
            .FirstOrDefaultAsync(cancellationToken);
        return totals is null
            ? new SalesTotalsDto(0, 0, 0, 0)
            : new SalesTotalsDto(totals.Count, totals.Total, totals.Discount, totals.Debt);
    }
}
