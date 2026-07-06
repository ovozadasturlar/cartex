using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Models;
using Cartex.Persistence;
using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Supplies.Queries;

public record GetSuppliesTotalsQuery : FilteringRequest, IRequest<SuppliesTotalsDto>
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public long? SupplierId { get; set; }
}

public record SuppliesTotalsDto(int Count, decimal TotalAmount);

public sealed class GetSuppliesTotalsQueryHandler(IApplicationDbContext db) : IRequestHandler<GetSuppliesTotalsQuery, SuppliesTotalsDto>
{
    public async Task<SuppliesTotalsDto> Handle(GetSuppliesTotalsQuery request, CancellationToken cancellationToken)
    {
        var query = db.Supplies.AsQueryable();

        if (request.FromDate is { } fromDate)
            query = query.Where(s => s.CreatedAt >= DateTime.SpecifyKind(fromDate, DateTimeKind.Utc));
        if (request.ToDate is { } toDate)
            query = query.Where(s => s.CreatedAt < DateTime.SpecifyKind(toDate, DateTimeKind.Utc));
        if (request.SupplierId is { } supplierId)
            query = query.Where(s => s.SupplierId == supplierId);

        query = query.AsFilterable(request);
        return new SuppliesTotalsDto(
            await query.CountAsync(cancellationToken),
            await query.SumAsync(s => (decimal?)s.TotalAmount, cancellationToken) ?? 0);
    }
}
