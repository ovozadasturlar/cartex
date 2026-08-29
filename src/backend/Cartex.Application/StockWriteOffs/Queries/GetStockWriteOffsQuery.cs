using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Shared.Models.Common;
using Cartex.Shared.Models.Stocks;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.StockWriteOffs.Queries;

public sealed record GetStockWriteOffsQuery(
    DateOnly? From = null,
    DateOnly? To = null,
    long? WarehouseId = null,
    StockWriteOffReason? Reason = null,
    int Page = 1,
    int PageSize = 50) : IRequest<IReadOnlyCollection<StockWriteOffDto>>;

public sealed class GetStockWriteOffsQueryHandler(
    IApplicationDbContext db,
    ISettingsService settings,
    IPagingMetadataWriter writer) : IRequestHandler<GetStockWriteOffsQuery, IReadOnlyCollection<StockWriteOffDto>>
{
    public async Task<IReadOnlyCollection<StockWriteOffDto>> Handle(GetStockWriteOffsQuery request, CancellationToken cancellationToken)
    {
        await WriteOffAccess.EnsureTrackedAsync(settings, cancellationToken);

        var query = db.StockWriteOffDocuments.AsNoTracking()
            .Where(x => (request.From == null || x.BusinessDate >= request.From)
                && (request.To == null || x.BusinessDate <= request.To)
                && (request.WarehouseId == null || x.WarehouseId == request.WarehouseId)
                && (request.Reason == null || x.Lines.Any(line => line.Reason == request.Reason)));

        var total = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(x => x.BusinessDate)
            .ThenByDescending(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new
            {
                x.Id,
                x.DocumentNumber,
                x.BusinessDate,
                x.CreatedAt,
                x.WarehouseId,
                WarehouseName = x.Warehouse.Name,
                UserName = x.User.FullName,
                x.TotalCost,
                SupplierClaimAmount = x.Lines
                    .Where(line => line.Disposition == InventoryDisposition.SupplierClaim)
                    .Sum(line => line.LineCost),
                x.ReversesDocumentId,
                x.Note,
                Lines = x.Lines
                    .OrderBy(line => line.Id)
                    .Select(line => new
                    {
                        line.Id,
                        line.VariantId,
                        ProductName = line.Variant.Product.Name,
                        line.StockId,
                        line.Quantity,
                        line.UnitCost,
                        line.LineCost,
                        line.Reason,
                        line.Disposition,
                        line.SupplierId,
                        SupplierName = line.Supplier == null ? null : line.Supplier.Name,
                        line.Note
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        writer.Write(new PagedListMetadata(total, request.Page, request.PageSize,
            (int)Math.Ceiling((double)total / request.PageSize)));

        return [.. rows.Select(x => new StockWriteOffDto(
            x.Id,
            x.DocumentNumber,
            x.BusinessDate,
            x.CreatedAt,
            x.WarehouseId,
            x.WarehouseName,
            x.UserName,
            x.TotalCost,
            x.SupplierClaimAmount,
            x.ReversesDocumentId,
            x.Note,
            [.. x.Lines.Select(line => new StockWriteOffLineDto(
                line.Id,
                line.VariantId,
                line.ProductName,
                line.StockId,
                line.Quantity,
                line.UnitCost,
                line.LineCost,
                line.Reason.ToString(),
                line.Disposition.ToString(),
                line.SupplierId,
                line.SupplierName,
                line.Note))]))];
    }
}

public sealed class GetStockWriteOffsQueryValidator : AbstractValidator<GetStockWriteOffsQuery>
{
    public GetStockWriteOffsQueryValidator()
    {
        RuleFor(x => x.WarehouseId).GreaterThan(0).When(x => x.WarehouseId is not null);
        RuleFor(x => x.Reason).IsInEnum().When(x => x.Reason is not null);
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, PagingRequest.MaxPageSize);
    }
}
