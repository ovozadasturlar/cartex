using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Sales.Queries;

public record GetSalesQuery : FilteringRequest, IRequest<IReadOnlyCollection<SaleDto>>;

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

public sealed class GetSalesQueryHandler(
    IApplicationDbContext db,
    IPagingMetadataWriter writer) : IRequestHandler<GetSalesQuery, IReadOnlyCollection<SaleDto>>
{
    public async Task<IReadOnlyCollection<SaleDto>> Handle(GetSalesQuery request, CancellationToken cancellationToken)
    {
        return await db.Sales
            .Include(s => s.Customer)
            .Include(s => s.User)
            .ToPagedListAsync(request,
                s => new SaleDto(
                    s.Id,
                    s.CreatedAt,
                    s.TotalAmount,
                    s.PaidCash,
                    s.PaidCard,
                    s.PaidBonus,
                    s.DebtAmount,
                    s.Status.ToString(),
                    s.Customer != null ? s.Customer.FullName : null,
                    s.User.FullName),
                writer, cancellationToken);
    }
}
