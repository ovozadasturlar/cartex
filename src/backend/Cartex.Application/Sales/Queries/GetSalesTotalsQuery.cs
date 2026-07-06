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

        return new SalesTotalsDto(
            await query.CountAsync(cancellationToken),
            await query.SumAsync(s => (decimal?)s.TotalAmount, cancellationToken) ?? 0,
            await query.SumAsync(s => (decimal?)s.DiscountAmount, cancellationToken) ?? 0,
            await query.SumAsync(s => (decimal?)s.DebtAmount, cancellationToken) ?? 0);
    }
}
