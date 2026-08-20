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
    public int? TzOffsetMinutes { get; set; }
}

public sealed class GetDailySalesQueryHandler(IApplicationDbContext db, ICurrentUser currentUser) : IRequestHandler<GetDailySalesQuery, List<DailySalesPointDto>>
{
    public async Task<List<DailySalesPointDto>> Handle(GetDailySalesQuery request, CancellationToken cancellationToken)
    {
        var query = db.Sales
            .AsNoTracking()
            .ApplySaleScope(request, currentUser, request.FromDate, request.ToDate, request.WarehouseId, null);

        // Kun chegarasi mahalliy vaqtda hisoblanadi; `timestamptz` ustidan kunlik guruhlashni
        // PostgreSQL'ga tarjima qilib bo'lmaydi, shuning uchun oraliq tortilib xotirada guruhlanadi.
        var rows = await query
            .Select(s => new { s.CreatedAt, s.TotalAmount })
            .ToListAsync(cancellationToken);

        var offset = TimeSpan.FromMinutes(request.TzOffsetMinutes
            ?? (int)TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow).TotalMinutes);

        return [.. rows
            .GroupBy(x => (x.CreatedAt + offset).Date)
            .Select(g => new DailySalesPointDto(g.Key, g.Count(), g.Sum(x => x.TotalAmount)))
            .OrderBy(x => x.Date)];
    }
}
