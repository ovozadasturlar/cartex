using Cartex.Application.Common.Models;
using Cartex.Application.Reports.Queries;
using Cartex.Persistence;
using Cartex.Domain.Common;
using Cartex.Domain.Enums;
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
        // HIS-06: bu qator boshqaruv panelidagi daromad bilan bir xil ta'rifda bo'lishi shart,
        // shuning uchun hisob `SalesReportMath` ustida quriladi — ikki ekran ikki xil son
        // ko'rsatmasin. Qamrov esa `ApplySaleScope` orqali (`RUXSAT-07`).
        var scoped = db.Sales
            .AsNoTracking()
            .ApplySaleScope(request, currentUser, request.FromDate, request.ToDate, request.WarehouseId, null)
            .Where(s => s.Status == SaleStatus.Completed || s.Status == SaleStatus.PartialReturn);

        var sales = await scoped
            .Select(s => new { s.Id, s.CreatedAt, s.DiscountAmount })
            .ToListAsync(cancellationToken);

        var items = await scoped
            .SelectMany(s => s.Items)
            .GroupBy(i => i.SaleId)
            .Select(g => new
            {
                SaleId = g.Key,
                Gross = g.Sum(x => x.Quantity * x.UnitPrice),
                NetGross = g.Sum(x => (x.Quantity - x.ReturnedQuantity) * x.UnitPrice)
            })
            .ToListAsync(cancellationToken);

        var grossBySale = items.ToDictionary(x => x.SaleId, x => x.Gross);
        var netBySale = items.ToDictionary(x => x.SaleId, x => x.NetGross);

        // Kun chegarasi mahalliy vaqtda hisoblanadi; `timestamptz` ustidan kunlik guruhlashni
        // PostgreSQL'ga tarjima qilib bo'lmaydi, shuning uchun oraliq tortilib xotirada guruhlanadi.
        var offset = TimeSpan.FromMinutes(request.TzOffsetMinutes
            ?? (int)TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow).TotalMinutes);

        return [.. sales
            .Select(s => new
            {
                Date = (s.CreatedAt + offset).Date,
                Revenue = netBySale.GetValueOrDefault(s.Id)
                    * (1 - SalesReportMath.DiscountRate(grossBySale.GetValueOrDefault(s.Id), s.DiscountAmount))
            })
            .GroupBy(x => x.Date)
            .Select(g => new DailySalesPointDto(g.Key, g.Count(), g.Sum(x => x.Revenue)))
            .OrderBy(x => x.Date)];
    }
}
