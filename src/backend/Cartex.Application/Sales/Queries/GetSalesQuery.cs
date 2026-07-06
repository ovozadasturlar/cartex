using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Persistence;
using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Sales.Queries;

public record GetSalesQuery : FilteringRequest, IRequest<IReadOnlyCollection<SaleDto>>
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public long? WarehouseId { get; set; }
}

public record SaleLineDto(
    long SaleItemId,
    string ProductName,
    decimal Quantity,
    decimal ReturnedQuantity,
    decimal UnitPrice);

public record SaleDto(
    long Id,
    DateTime SaleDate,
    decimal TotalAmount,
    decimal PaidCash,
    decimal PaidCard,
    decimal PaidBonus,
    decimal DebtAmount,
    string Status,
    string ReceiptToken,
    string? CustomerName,
    string UserName,
    List<SaleLineDto> Items);

public sealed class GetSalesQueryHandler(
    IApplicationDbContext db,
    IPagingMetadataWriter writer) : IRequestHandler<GetSalesQuery, IReadOnlyCollection<SaleDto>>
{
    public async Task<IReadOnlyCollection<SaleDto>> Handle(GetSalesQuery request, CancellationToken cancellationToken)
    {
        var query = db.Sales
            .Include(s => s.Customer)
            .Include(s => s.User)
            .AsQueryable();

        if (request.FromDate is { } fromDate)
            query = query.Where(s => s.CreatedAt >= DateTime.SpecifyKind(fromDate, DateTimeKind.Utc));
        if (request.ToDate is { } toDate)
            query = query.Where(s => s.CreatedAt < DateTime.SpecifyKind(toDate, DateTimeKind.Utc));
        if (request.WarehouseId is { } warehouseId)
            query = query.Where(s => s.WarehouseId == warehouseId);

        return await query
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
                    s.ReceiptToken,
                    s.Customer != null ? s.Customer.FullName : null,
                    s.User.FullName,
                    s.Items.Select(i => new SaleLineDto(
                        i.Id,
                        i.Variant.Product.Name,
                        i.Quantity,
                        i.ReturnedQuantity,
                        i.UnitPrice)).ToList()),
                writer, cancellationToken);
    }
}
