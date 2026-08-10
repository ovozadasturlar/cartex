using Cartex.Application.Common.Interfaces;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Shared.Models.TradeCases;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.TradeCases.Queries;

public sealed record GetTradeCaseStatementQuery(long TradeCaseId, DateTime? From = null, DateTime? To = null)
    : IRequest<TradeCaseStatementDto>;

file sealed record RawEntry(
    DateTime OccurredAt,
    int Order,
    string Type,
    long DocumentId,
    string DocumentNumber,
    string Summary,
    decimal Debit,
    decimal Credit,
    string Currency,
    long? SaleId = null,
    string? ReceiptToken = null);

public sealed class GetTradeCaseStatementQueryHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser) : IRequestHandler<GetTradeCaseStatementQuery, TradeCaseStatementDto>
{
    public async Task<TradeCaseStatementDto> Handle(GetTradeCaseStatementQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(AppPermissions.Statements.View))
            throw new ForbiddenException("Hisob ko'chirmasini ko'rishga ruxsat yo'q.");

        var query = db.TradeCases.AsNoTracking().Where(x => x.Id == request.TradeCaseId);
        if (!currentUser.HasPermission(AppPermissions.Customers.ViewAll))
            query = query.Where(x => x.Customer.AssignedUserId == currentUser.UserId);
        var header = await query.Select(x => new
            {
                x.Id, x.CaseNumber, x.Title, x.CustomerId,
                CustomerName = x.Customer.FullName,
                BaseCurrency = x.Branch.Business.Currency,
                x.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Trade case not found.", "trade_case_not_found");

        var all = new List<RawEntry>();

        var issues = await db.GoodsIssueDocuments.AsNoTracking()
            .Where(x => x.TradeCaseId == request.TradeCaseId && x.Status == BusinessDocumentStatus.Posted)
            .Select(x => new
            {
                x.Id, x.DocumentNumber, x.CreatedAt, x.EstimatedAmount,
                Quantity = x.Lines.Sum(l => l.Quantity)
            }).ToListAsync(cancellationToken);
        all.AddRange(issues.Select(x => new RawEntry(x.CreatedAt, 10, "GoodsIssue", x.Id,
            x.DocumentNumber, $"Mahsulot berildi: {x.Quantity:0.###}", 0, 0, header.BaseCurrency)));

        var goodsReturns = await db.GoodsReturnDocuments.AsNoTracking()
            .Where(x => x.TradeCaseId == request.TradeCaseId && x.Status == BusinessDocumentStatus.Posted)
            .Select(x => new { x.Id, x.DocumentNumber, x.CreatedAt, Quantity = x.Lines.Sum(l => l.Quantity) })
            .ToListAsync(cancellationToken);
        all.AddRange(goodsReturns.Select(x => new RawEntry(x.CreatedAt, 20, "GoodsReturn", x.Id,
            x.DocumentNumber, $"Mahsulot qaytdi: {x.Quantity:0.###}", 0, 0, header.BaseCurrency)));

        var settlements = await db.TradeCaseSettlements.AsNoTracking()
            .Where(x => x.TradeCaseId == request.TradeCaseId && x.Status == BusinessDocumentStatus.Posted)
            .Select(x => new
            {
                x.Id, x.DocumentNumber, x.CreatedAt, x.SaleId, x.Sale.ReceiptToken,
                x.Sale.TotalAmount, x.Sale.PaidCash, x.Sale.PaidCard, x.Sale.PaidBonus
            }).ToListAsync(cancellationToken);
        foreach (var row in settlements)
        {
            all.Add(new RawEntry(row.CreatedAt, 30, "Settlement", row.Id, row.DocumentNumber,
                "Ishlatilgan mahsulotlar hisoblandi", row.TotalAmount, 0, header.BaseCurrency,
                row.SaleId, row.ReceiptToken));
            var directPayment = row.PaidCash + row.PaidCard + row.PaidBonus;
            if (directPayment > 0)
                all.Add(new RawEntry(row.CreatedAt, 31, "SalePayment", row.Id, row.DocumentNumber,
                    "Hisob paytida qabul qilingan to'lov", 0, directPayment, header.BaseCurrency,
                    row.SaleId, row.ReceiptToken));
        }

        var directSales = await db.Sales.AsNoTracking()
            .Where(x => x.TradeCaseId == request.TradeCaseId
                        && !db.TradeCaseSettlements.Any(s => s.SaleId == x.Id)
                        && x.Status == SaleStatus.Completed)
            .Select(x => new
            {
                x.Id, x.ReceiptToken, x.CreatedAt,
                x.TotalAmount, x.PaidCash, x.PaidCard, x.PaidBonus
            }).ToListAsync(cancellationToken);
        foreach (var row in directSales)
        {
            all.Add(new RawEntry(row.CreatedAt, 35, "DirectSale", row.Id, row.ReceiptToken,
                "Savdo amalga oshirildi (Kassa)", row.TotalAmount, 0, header.BaseCurrency,
                row.Id, row.ReceiptToken));
            var directPayment = row.PaidCash + row.PaidCard + row.PaidBonus;
            if (directPayment > 0)
                all.Add(new RawEntry(row.CreatedAt, 36, "SalePayment", row.Id, row.ReceiptToken,
                    "Kassada qabul qilingan to'lov", 0, directPayment, header.BaseCurrency,
                    row.Id, row.ReceiptToken));
        }

        var payments = await db.CustomerPaymentDocuments.AsNoTracking()
            .Where(x => x.TradeCaseId == request.TradeCaseId && x.Status == BusinessDocumentStatus.Posted)
            .Select(x => new { x.Id, x.DocumentNumber, x.CreatedAt, x.TotalBaseAmount })
            .ToListAsync(cancellationToken);
        all.AddRange(payments.Select(x => new RawEntry(x.CreatedAt, 40, "CustomerPayment", x.Id,
            x.DocumentNumber, "Mijoz to'lovi", 0, x.TotalBaseAmount, header.BaseCurrency)));

        var saleReturns = await db.CustomerReturnDocuments.AsNoTracking()
            .Where(x => x.Sale.TradeCaseId == request.TradeCaseId && x.Status == BusinessDocumentStatus.Posted)
            .Select(x => new
            {
                x.Id, x.DocumentNumber, x.CreatedAt, x.SaleId, x.Sale.ReceiptToken, x.RefundAmount,
                CashOut = x.Settlements.Where(s => s.Method == ReturnSettlementMethod.Cash
                                                   || s.Method == ReturnSettlementMethod.Card)
                    .Sum(s => (decimal?)s.AmountBase) ?? 0,
                NoCharge = x.Settlements.Where(s => s.Method == ReturnSettlementMethod.NoCharge)
                    .Sum(s => (decimal?)s.AmountBase) ?? 0
            }).ToListAsync(cancellationToken);
        foreach (var row in saleReturns)
        {
            all.Add(new RawEntry(row.CreatedAt, 50, "SaleReturn", row.Id, row.DocumentNumber,
                "Savdo qaytaruvi", 0, row.RefundAmount, header.BaseCurrency,
                row.SaleId, row.ReceiptToken));
            var cancelledCredit = row.CashOut + row.NoCharge;
            if (cancelledCredit > 0)
                all.Add(new RawEntry(row.CreatedAt, 51, "CustomerRefund", row.Id, row.DocumentNumber,
                    row.CashOut > 0 ? "Mijozga pul qaytarildi" : "Moliyaviy hisobga olinmadi",
                    cancelledCredit, 0, header.BaseCurrency, row.SaleId, row.ReceiptToken));
        }

        var ordered = all.OrderBy(x => x.OccurredAt).ThenBy(x => x.Order).ThenBy(x => x.DocumentId).ToList();
        var opening = ordered.Where(x => request.From.HasValue && x.OccurredAt < request.From.Value)
            .Sum(x => x.Debit - x.Credit);
        var inRange = ordered.Where(x => (!request.From.HasValue || x.OccurredAt >= request.From.Value)
                                         && (!request.To.HasValue || x.OccurredAt < request.To.Value))
            .ToList();
        var running = opening;
        var timeline = inRange.Select(x =>
        {
            running += x.Debit - x.Credit;
            return new TradeCaseStatementEntryDto(x.OccurredAt, x.Type, x.DocumentId,
                x.DocumentNumber, x.Summary, x.Debit, x.Credit, running, x.Currency,
                x.SaleId, x.ReceiptToken);
        }).ToList();

        var from = request.From;
        var to = request.To;
        var issueLines = await db.GoodsIssueLines.AsNoTracking()
            .Where(x => x.Document.TradeCaseId == request.TradeCaseId
                        && x.Document.Status == BusinessDocumentStatus.Posted
                        && (!from.HasValue || x.Document.CreatedAt >= from.Value)
                        && (!to.HasValue || x.Document.CreatedAt < to.Value))
            .Select(x => new
            {
                x.VariantId, ProductName = x.Variant.Product.Name,
                UnitName = x.Variant.Product.Unit.ShortName,
                x.Quantity, x.UnitPrice
            }).ToListAsync(cancellationToken);

        var returned = await db.GoodsReturnLines.AsNoTracking()
            .Where(x => x.Document.TradeCaseId == request.TradeCaseId
                        && x.Document.Status == BusinessDocumentStatus.Posted
                        && (!from.HasValue || x.Document.CreatedAt >= from.Value)
                        && (!to.HasValue || x.Document.CreatedAt < to.Value))
            .GroupBy(x => x.VariantId)
            .Select(x => new
            {
                VariantId = x.Key,
                Sellable = x.Where(l => l.Disposition == InventoryDisposition.SellableRestock).Sum(l => l.Quantity),
                NonSellable = x.Where(l => l.Disposition != InventoryDisposition.SellableRestock).Sum(l => l.Quantity)
            }).ToListAsync(cancellationToken);

        var settled = await db.SaleItems.AsNoTracking()
            .Where(x => x.Sale.TradeCaseId == request.TradeCaseId
                        && x.Sale.Status == SaleStatus.Completed
                        && (!from.HasValue || x.Sale.CreatedAt >= from.Value)
                        && (!to.HasValue || x.Sale.CreatedAt < to.Value))
            .GroupBy(x => new { x.VariantId, ProductName = x.Variant.Product.Name, UnitName = x.Variant.Product.Unit.ShortName })
            .Select(x => new
            {
                x.Key.VariantId,
                x.Key.ProductName,
                x.Key.UnitName,
                Quantity = x.Sum(l => l.Quantity),
                Amount = x.Sum(l => l.Quantity * l.UnitPrice)
            }).ToListAsync(cancellationToken);

        var returnedByVariant = returned.ToDictionary(x => x.VariantId);
        var settledByVariant = settled.ToDictionary(x => x.VariantId);
        var issueByVariant = issueLines.GroupBy(x => new { x.VariantId, x.ProductName, x.UnitName })
            .ToDictionary(g => g.Key.VariantId, g => new
            {
                g.Key.ProductName,
                g.Key.UnitName,
                Issued = g.Sum(x => x.Quantity),
                Weighted = g.Sum(x => x.Quantity * x.UnitPrice)
            });

        var allVariantIds = issueByVariant.Keys
            .Union(settledByVariant.Keys)
            .Union(returnedByVariant.Keys)
            .Distinct()
            .ToList();

        var products = new List<TradeCaseStatementProductDto>();
        foreach (var variantId in allVariantIds)
        {
            issueByVariant.TryGetValue(variantId, out var issueInfo);
            settledByVariant.TryGetValue(variantId, out var saleInfo);
            returnedByVariant.TryGetValue(variantId, out var retInfo);

            var productName = issueInfo?.ProductName ?? saleInfo?.ProductName ?? "Mahsulot";
            var unitName = issueInfo?.UnitName ?? saleInfo?.UnitName ?? "dona";
            var issued = issueInfo?.Issued ?? 0;
            var sellable = retInfo?.Sellable ?? 0;
            var nonSellable = retInfo?.NonSellable ?? 0;
            var settledQty = saleInfo?.Quantity ?? 0;
            var chargedAmount = saleInfo?.Amount ?? 0;

            decimal avgPrice;
            if (issued > 0)
                avgPrice = Math.Round((issueInfo?.Weighted ?? 0) / issued, 2);
            else if (settledQty > 0)
                avgPrice = Math.Round(chargedAmount / settledQty, 2);
            else
                avgPrice = 0;

            var custody = issued - sellable - nonSellable - settledQty;
            products.Add(new TradeCaseStatementProductDto(
                variantId, productName, unitName, issued, sellable, nonSellable, settledQty,
                custody, avgPrice, chargedAmount));
        }

        return new TradeCaseStatementDto(header.Id, header.CaseNumber, header.Title,
            header.CustomerId, header.CustomerName, request.From, request.To,
            header.BaseCurrency, opening, running, timeline, products.OrderBy(x => x.ProductName).ToList(), DateTime.UtcNow);
    }
}
