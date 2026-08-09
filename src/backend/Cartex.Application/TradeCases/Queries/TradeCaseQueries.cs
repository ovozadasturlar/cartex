using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Shared.Models.TradeCases;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.TradeCases.Queries;

public sealed record GetTradeCasesQuery : FilteringRequest, IRequest<IReadOnlyCollection<TradeCaseListDto>>
{
    public long? CustomerId { get; init; }
    public long? WarehouseId { get; init; }
    public TradeCaseStatus? Status { get; init; }
}

public sealed class GetTradeCasesQueryHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IPagingMetadataWriter writer) : IRequestHandler<GetTradeCasesQuery, IReadOnlyCollection<TradeCaseListDto>>
{
    public async Task<IReadOnlyCollection<TradeCaseListDto>> Handle(GetTradeCasesQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(AppPermissions.TradeCases.View))
            throw new ForbiddenException("Loyihalarni ko'rishga ruxsat yo'q.");

        var query = db.TradeCases.AsNoTracking();
        if (!currentUser.HasPermission(AppPermissions.Customers.ViewAll))
            query = query.Where(x => x.Customer.AssignedUserId == currentUser.UserId);
        if (request.CustomerId is { } customerId) query = query.Where(x => x.CustomerId == customerId);
        if (request.WarehouseId is { } warehouseId) query = query.Where(x => x.WarehouseId == warehouseId);
        if (request.Status is { } status) query = query.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(x => EF.Functions.ILike(x.CaseNumber, $"%{search}%")
                                     || EF.Functions.ILike(x.Title, $"%{search}%")
                                     || EF.Functions.ILike(x.Customer.FullName, $"%{search}%"));
        }

        return await query.ToPagedListAsync(request, x => new TradeCaseListDto(
            x.Id,
            x.CaseNumber,
            x.BusinessDate,
            x.Title,
            x.CustomerId,
            x.Customer.FullName,
            x.WarehouseId,
            x.Warehouse.Name,
            x.Workflow.ToString(),
            x.PricePolicy.ToString(),
            x.Status.ToString(),
            x.Issues.Where(d => d.Status == BusinessDocumentStatus.Posted)
                .SelectMany(d => d.Lines).Sum(l => (decimal?)l.Quantity) ?? 0,
            x.Issues.Where(d => d.Status == BusinessDocumentStatus.Posted)
                .SelectMany(d => d.Lines).Sum(l => (decimal?)l.ReturnedQuantity) ?? 0,
            x.Issues.Where(d => d.Status == BusinessDocumentStatus.Posted)
                .SelectMany(d => d.Lines).Sum(l => (decimal?)l.SettledQuantity) ?? 0,
            x.Issues.Where(d => d.Status == BusinessDocumentStatus.Posted)
                .SelectMany(d => d.Lines).Sum(l => (decimal?)(l.Quantity - l.ReturnedQuantity - l.SettledQuantity)) ?? 0,
            x.Issues.Where(d => d.Status == BusinessDocumentStatus.Posted)
                .SelectMany(d => d.Lines).Sum(l => (decimal?)((l.Quantity - l.ReturnedQuantity - l.SettledQuantity) * l.UnitPrice)) ?? 0,
            x.Currency,
            x.Version,
            x.UpdatedAt ?? x.CreatedAt), writer, cancellationToken);
    }
}

public sealed record GetTradeCaseByIdQuery(long Id) : IRequest<TradeCaseDetailDto>;

public sealed class GetTradeCaseByIdQueryHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser) : IRequestHandler<GetTradeCaseByIdQuery, TradeCaseDetailDto>
{
    public async Task<TradeCaseDetailDto> Handle(GetTradeCaseByIdQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(AppPermissions.TradeCases.View))
            throw new ForbiddenException("Loyihalarni ko'rishga ruxsat yo'q.");

        var query = db.TradeCases.AsNoTracking().Where(x => x.Id == request.Id);
        if (!currentUser.HasPermission(AppPermissions.Customers.ViewAll))
            query = query.Where(x => x.Customer.AssignedUserId == currentUser.UserId);
        var header = await query.Select(x => new
            {
                x.Id, x.CaseNumber, x.BusinessDate, x.Title, x.SiteAddress,
                x.CustomerId, CustomerName = x.Customer.FullName, CustomerPhone = x.Customer.Phone,
                x.BranchId, BranchName = x.Branch.Name, x.WarehouseId, WarehouseName = x.Warehouse.Name,
                x.Workflow, x.PricePolicy, x.Status, x.Currency, x.Note, x.Version,
                x.CreatedAt, UpdatedAt = x.UpdatedAt ?? x.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Trade case not found.", "trade_case_not_found");

        var lines = await db.GoodsIssueLines.AsNoTracking()
            .Where(x => x.Document.TradeCaseId == request.Id && x.Document.Status == BusinessDocumentStatus.Posted)
            .OrderBy(x => x.Document.BusinessDate).ThenBy(x => x.Id)
            .Select(x => new TradeCaseLineDto(
                x.Id, x.GoodsIssueDocumentId, x.Document.DocumentNumber, x.Document.BusinessDate,
                x.VariantId, x.Variant.Product.Name, x.Variant.Product.Unit.ShortName,
                x.Quantity, x.ReturnedQuantity, x.SettledQuantity,
                x.Quantity - x.ReturnedQuantity - x.SettledQuantity,
                x.UnitPrice, x.PriceCurrency, x.PriceRate,
                x.Variant.Barcodes.OrderBy(b => b.Id).Select(b => b.Code).FirstOrDefault(),
                x.Variant.Product.QuantityStepOverride ?? x.Variant.Product.Unit.DefaultQuantityStep,
                (x.Variant.Product.QuantityStepOverride ?? x.Variant.Product.Unit.DefaultQuantityStep)
                    != decimal.Truncate(x.Variant.Product.QuantityStepOverride ?? x.Variant.Product.Unit.DefaultQuantityStep)))
            .ToListAsync(cancellationToken);

        var documents = new List<TradeCaseDocumentDto>();
        documents.AddRange(await db.GoodsIssueDocuments.AsNoTracking()
            .Where(x => x.TradeCaseId == request.Id)
            .Select(x => new TradeCaseDocumentDto("GoodsIssue", x.Id, x.DocumentNumber,
                x.BusinessDate, x.CreatedAt, x.Status.ToString(), x.Lines.Sum(l => l.Quantity),
                x.EstimatedAmount, x.Note, null, null))
            .ToListAsync(cancellationToken));
        documents.AddRange(await db.GoodsReturnDocuments.AsNoTracking()
            .Where(x => x.TradeCaseId == request.Id)
            .Select(x => new TradeCaseDocumentDto("GoodsReturn", x.Id, x.DocumentNumber,
                x.BusinessDate, x.CreatedAt, x.Status.ToString(), x.Lines.Sum(l => l.Quantity),
                0, x.Note, null, null))
            .ToListAsync(cancellationToken));
        documents.AddRange(await db.TradeCaseSettlements.AsNoTracking()
            .Where(x => x.TradeCaseId == request.Id)
            .Select(x => new TradeCaseDocumentDto("Settlement", x.Id, x.DocumentNumber,
                x.BusinessDate, x.CreatedAt, x.Status.ToString(), x.Sale.Items.Sum(i => i.Quantity),
                x.Amount, x.Note, x.SaleId, x.Sale.ReceiptToken))
            .ToListAsync(cancellationToken));
        documents.AddRange(await db.CustomerPaymentDocuments.AsNoTracking()
            .Where(x => x.TradeCaseId == request.Id)
            .Select(x => new TradeCaseDocumentDto("CustomerPayment", x.Id, x.DocumentNumber,
                x.BusinessDate, x.CreatedAt, x.Status.ToString(), 0,
                x.TotalBaseAmount, x.Note, null, null))
            .ToListAsync(cancellationToken));

        var isOpen = header.Status is TradeCaseStatus.Open or TradeCaseStatus.SettlementPending;
        var hasCustody = lines.Any(x => x.CustodyQuantity > 0);
        var hasDocuments = documents.Any(x => x.Type is "GoodsIssue" or "GoodsReturn" or "Settlement");
        var participants = await db.TradeCaseParticipants.AsNoTracking()
            .Where(x => x.TradeCaseId == request.Id)
            .OrderBy(x => x.Id)
            .Select(x => new TradeCaseParticipantDto(x.RoleDefinitionId, x.PartyId,
                x.RoleLabelSnapshot, x.PartyNameSnapshot, x.PartyPhoneSnapshot))
            .ToListAsync(cancellationToken);
        var allowed = new TradeCaseAllowedActions(
            isOpen && currentUser.HasPermission(AppPermissions.TradeCases.Edit),
            header.Status == TradeCaseStatus.Open && currentUser.HasPermission(AppPermissions.GoodsIssues.Create),
            isOpen && hasCustody && currentUser.HasPermission(AppPermissions.GoodsIssues.Return),
            isOpen && hasCustody && currentUser.HasPermission(AppPermissions.TradeCases.Settle),
            isOpen && !hasDocuments && currentUser.HasPermission(AppPermissions.TradeCases.Close),
            isOpen && currentUser.HasPermission(AppPermissions.CustomerPayments.Create),
            currentUser.HasPermission(AppPermissions.Statements.Export),
            isOpen && !hasCustody && currentUser.HasPermission(AppPermissions.TradeCases.Close));

        return new TradeCaseDetailDto(header.Id, header.CaseNumber, header.BusinessDate,
            header.Title, header.SiteAddress, header.CustomerId, header.CustomerName,
            header.CustomerPhone, header.BranchId, header.BranchName, header.WarehouseId,
            header.WarehouseName, header.Workflow.ToString(), header.PricePolicy.ToString(),
            header.Status.ToString(), header.Currency, header.Note, header.Version,
            header.CreatedAt, header.UpdatedAt, lines,
            documents.OrderByDescending(x => x.CreatedAt).ToList(), allowed, participants);
    }
}
