using MediatR;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;

namespace Cartex.Application.Sales.Queries;

public record GetSalesQuery(long? WarehouseId, DateTime? FromDate, DateTime? ToDate) : IRequest<List<SaleDto>>;

public record SaleDto(
    long Id,
    DateTime SaleDate,
    decimal TotalAmount,
    decimal PaidCash,
    decimal PaidCard,
    decimal PaidBonus,
    decimal DebtAmount,
    string Status,
    string? CustomerName,
    string UserName);

public sealed class GetSalesQueryHandler(IApplicationDbContext db) : IRequestHandler<GetSalesQuery, List<SaleDto>>
{
    public async Task<List<SaleDto>> Handle(GetSalesQuery request, CancellationToken cancellationToken)
    {
        var query = db.Sales
            .Include(s => s.Customer)
            .Include(s => s.User)
            .AsQueryable();

        if (request.WarehouseId is not null)
            query = query.Where(s => s.WarehouseId == request.WarehouseId);

        if (request.FromDate is not null)
            query = query.Where(s => s.CreatedAt >= request.FromDate);

        if (request.ToDate is not null)
            query = query.Where(s => s.CreatedAt <= request.ToDate);

        return await query
            .Select(s => new SaleDto(
                s.Id,
                s.CreatedAt,
                s.TotalAmount,
                s.PaidCash,
                s.PaidCard,
                s.PaidBonus,
                s.DebtAmount,
                s.Status.ToString(),
                s.Customer != null ? s.Customer.FullName : null,
                s.User.FullName))
            .ToListAsync(cancellationToken);
    }
}
