using Cartex.Application.Common.Models;
using Cartex.Persistence;
using Cartex.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Cartex.Shared.Models.Sales;

namespace Cartex.Application.Sales.Queries;

public record GetDailySalesQuery : FilteringRequest, IRequest<List<DailySalesPointDto>>
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public long? WarehouseId { get; set; }
}

public sealed class GetDailySalesQueryHandler(IApplicationDbContext db, ICurrentUser currentUser) : IRequestHandler<GetDailySalesQuery, List<DailySalesPointDto>>
{
    public async Task<List<DailySalesPointDto>> Handle(GetDailySalesQuery request, CancellationToken cancellationToken)
    {
        var query = db.Sales
            .AsNoTracking()
            .ApplySaleScope(request, currentUser, request.FromDate, request.ToDate, request.WarehouseId, null);

        return await query
            .GroupBy(s => s.CreatedAt.Date)
            .Select(g => new DailySalesPointDto(g.Key, g.Count(), g.Sum(s => s.TotalAmount)))
            .OrderBy(x => x.Date)
            .ToListAsync(cancellationToken);
    }
}
