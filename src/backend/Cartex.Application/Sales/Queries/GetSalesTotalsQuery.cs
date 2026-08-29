using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Models;
using Cartex.Persistence;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Cartex.Shared.Models.Sales;

namespace Cartex.Application.Sales.Queries;

public record GetSalesTotalsQuery : FilteringRequest, IRequest<SalesTotalsDto>
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public long? WarehouseId { get; set; }
    public long? CustomerId { get; set; }
}

public sealed class GetSalesTotalsQueryHandler(IApplicationDbContext db, ICurrentUser currentUser) : IRequestHandler<GetSalesTotalsQuery, SalesTotalsDto>
{
    public async Task<SalesTotalsDto> Handle(GetSalesTotalsQuery request, CancellationToken cancellationToken)
    {
        var query = db.Sales
            .AsNoTracking()
            .ApplySaleScope(request, currentUser, request.FromDate, request.ToDate,
                request.WarehouseId, request.CustomerId);
        var filtering = request with { };
        filtering.WithoutSearch();
        query = query.AsFilterable(filtering);

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
