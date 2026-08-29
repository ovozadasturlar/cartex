using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Persistence;
using Cartex.Shared.Models.Sales;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.CustomerReturns.Queries;

public record GetCustomerReturnsQuery : FilteringRequest, IRequest<IReadOnlyCollection<CustomerReturnListDto>>
{
    public long? CustomerId { get; init; }
    public long? SaleId { get; init; }
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
}

public sealed class GetCustomerReturnsQueryHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IPagingMetadataWriter writer) : IRequestHandler<GetCustomerReturnsQuery, IReadOnlyCollection<CustomerReturnListDto>>
{
    public async Task<IReadOnlyCollection<CustomerReturnListDto>> Handle(
        GetCustomerReturnsQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(AppPermissions.Returns.View))
            throw new ForbiddenException("Qaytaruvlarni ko'rishga ruxsat yo'q.");

        var query = db.CustomerReturnDocuments.AsNoTracking();
        if (!currentUser.CanAccessAllBranches)
            query = query.Where(x => currentUser.BranchIds.Contains(x.BranchId));
        if (!currentUser.HasPermission(AppPermissions.Customers.ViewAll))
            query = query.Where(x => x.Customer == null || x.Customer.AssignedUserId == currentUser.UserId);
        if (request.CustomerId is { } customerId)
            query = query.Where(x => x.CustomerId == customerId);
        if (request.SaleId is { } saleId)
            query = query.Where(x => x.Lines.Any(line => line.SaleId == saleId));
        if (request.FromDate is { } from)
            query = query.Where(x => x.BusinessDate >= from);
        if (request.ToDate is { } to)
            query = query.Where(x => x.BusinessDate <= to);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(x => EF.Functions.ILike(x.DocumentNumber, $"%{search}%")
                                     || (x.Customer != null && EF.Functions.ILike(x.Customer.Party.FullName, $"%{search}%")));
        }

        return await query.ToPagedListAsync(request, x => new CustomerReturnListDto(
            x.Id,
            x.DocumentNumber,
            x.CustomerId,
            x.Customer == null ? null : x.Customer.Party.FullName,
            x.BusinessDate,
            x.CreatedAt,
            x.Status.ToString(),
            x.Lines.Count,
            x.RefundAmount,
            x.Note), writer, cancellationToken);
    }
}

public record GetCustomerReturnByIdQuery(long Id) : IRequest<CustomerReturnDocumentDto>;

public sealed class GetCustomerReturnByIdQueryHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser) : IRequestHandler<GetCustomerReturnByIdQuery, CustomerReturnDocumentDto>
{
    public async Task<CustomerReturnDocumentDto> Handle(GetCustomerReturnByIdQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(AppPermissions.Returns.View))
            throw new ForbiddenException("Qaytaruvlarni ko'rishga ruxsat yo'q.");

        var query = db.CustomerReturnDocuments.AsNoTracking().Where(x => x.Id == request.Id);
        if (!currentUser.CanAccessAllBranches)
            query = query.Where(x => currentUser.BranchIds.Contains(x.BranchId));
        if (!currentUser.HasPermission(AppPermissions.Customers.ViewAll))
            query = query.Where(x => x.Customer == null || x.Customer.AssignedUserId == currentUser.UserId);

        var document = await query
            .Include(x => x.Customer).ThenInclude(x => x!.Party)
            .Include(x => x.Warehouse)
            .Include(x => x.User)
            .Include(x => x.Lines).ThenInclude(x => x.Variant).ThenInclude(x => x.Product).ThenInclude(x => x.Unit)
            .Include(x => x.Settlements)
            .AsSplitQuery()
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Return document not found.", "return_document_not_found");

        return new CustomerReturnDocumentDto(
            document.Id,
            document.DocumentNumber,
            document.BranchId,
            document.WarehouseId,
            document.Warehouse.Name,
            document.CustomerId,
            document.Customer?.Party.FullName,
            document.UserId,
            document.User.FullName,
            document.BusinessDate,
            document.CreatedAt,
            document.Status.ToString(),
            document.GrossAmount,
            document.RefundAmount,
            document.CashbackReversed,
            document.Note,
            document.Lines.Select(x => new CustomerReturnLineDto(
                x.Id,
                x.SaleId,
                x.SaleItemId,
                x.VariantId,
                x.Variant.Product.Name,
                x.Variant.Product.Unit.ShortName,
                x.Quantity,
                x.UnitPrice,
                x.LineAmount,
                x.CashbackReversed,
                x.Reason,
                x.Condition.ToString(),
                x.Disposition.ToString())).ToList(),
            document.Settlements.Select(x => new CustomerReturnSettlementDto(
                x.Method.ToString(), x.Currency, x.Amount, x.Rate, x.AmountBase)).ToList());
    }
}
