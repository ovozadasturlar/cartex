using Cartex.Application.Common.Documents;
using Cartex.Application.Common.Finance;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Inventory;
using Cartex.Application.Common.Measurement;
using Cartex.Application.Common.Settings;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Shared.Models.TradeCases;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.TradeCases.Commands;

public sealed record GoodsIssueLineInput(long VariantId, decimal Quantity, decimal? UnitPrice = null);

public sealed record CreateGoodsIssueCommand(
    long TradeCaseId,
    List<GoodsIssueLineInput> Lines,
    DateOnly? BusinessDate = null,
    string? Note = null,
    string? IdempotencyKey = null,
    int? ExpectedCaseVersion = null) : ICommand<GoodsIssueCreatedDto>;

file sealed record IssuePrice(decimal AmountBase, string Currency, decimal Rate);

public sealed class CreateGoodsIssueCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IStockAllocator stockAllocator,
    IQuantityPolicyService quantityPolicy,
    ICurrencyService currency,
    ISettingsService settings,
    IBranchCatalogService branchCatalog,
    IAuditService audit) : IRequestHandler<CreateGoodsIssueCommand, GoodsIssueCreatedDto>
{
    public async Task<GoodsIssueCreatedDto> Handle(CreateGoodsIssueCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("Not authenticated.");
        if (!currentUser.HasPermission(AppPermissions.GoodsIssues.Create))
            throw new ForbiddenException("Mahsulotni saqlovga berishga ruxsat yo'q.");

        var tradeCase = await db.TradeCases
            .FromSqlInterpolated($"SELECT * FROM trade_cases WHERE id = {request.TradeCaseId} FOR UPDATE")
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Trade case not found.", "trade_case_not_found");

        var idempotencyKey = NormalizeOptional(request.IdempotencyKey);
        if (idempotencyKey is not null)
        {
            var existing = await db.GoodsIssueDocuments
                .Where(x => x.BranchId == tradeCase.BranchId && x.IdempotencyKey == idempotencyKey)
                .Select(x => new GoodsIssueCreatedDto(x.Id, x.DocumentNumber, x.EstimatedAmount, x.TradeCase.Version))
                .FirstOrDefaultAsync(cancellationToken);
            if (existing is not null) return existing;
        }

        EnsureEditable(tradeCase, request.ExpectedCaseVersion);
        if (tradeCase.Workflow != TradeCaseWorkflow.CustodyUntilSettlement)
            throw new BusinessRuleException("Bu jarayonda mahsulot oddiy savdo orqali beriladi.", "custody_workflow_required");

        var variantIds = request.Lines.Select(x => x.VariantId).Distinct().ToList();
        await quantityPolicy.ValidateAsync(request.Lines.Select(x => (x.VariantId, x.Quantity)), cancellationToken);
        var variants = await db.ProductVariants
            .Where(x => variantIds.Contains(x.Id) && x.Product.IsEnabled)
            .Select(x => new { x.Id, x.ProductId, ProductName = x.Product.Name })
            .ToListAsync(cancellationToken);
        if (variants.Count != variantIds.Count)
            throw new BusinessRuleException("Mahsulot topilmadi yoki berish uchun yopilgan.", "variant_unavailable");
        if (request.Lines.Any(x => x.UnitPrice.HasValue)
            && !currentUser.HasPermission(AppPermissions.Sales.PriceOverride))
            throw new ForbiddenException("Kelishilgan narxni kiritishga ruxsat yo'q.");

        var baseCode = (await currency.BaseAsync(cancellationToken)).ToUpperInvariant();
        var prices = await db.ProductPrices
            .Where(x => variantIds.Contains(x.VariantId)
                        && (x.WarehouseId == tradeCase.WarehouseId || x.WarehouseId == null))
            .ToListAsync(cancellationToken);
        var rateCache = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            [baseCode] = 1m
        };

        async Task<decimal> RateAsync(string code)
        {
            code = code.ToUpperInvariant();
            if (rateCache.TryGetValue(code, out var value)) return value;
            value = await currency.RateAsync(code, cancellationToken);
            rateCache[code] = value;
            return value;
        }

        var resolvedPrices = new Dictionary<long, IssuePrice>();
        foreach (var line in request.Lines)
        {
            if (resolvedPrices.ContainsKey(line.VariantId)) continue;
            if (line.UnitPrice is { } entered)
            {
                var rate = await RateAsync(tradeCase.Currency);
                resolvedPrices[line.VariantId] = new IssuePrice(
                    Math.Round(entered * rate, 2), tradeCase.Currency, rate);
                continue;
            }

            var price = prices.FirstOrDefault(x => x.VariantId == line.VariantId && x.WarehouseId == tradeCase.WarehouseId)
                        ?? prices.FirstOrDefault(x => x.VariantId == line.VariantId && x.WarehouseId == null)
                        ?? throw new BusinessRuleException(
                            $"{variants.First(x => x.Id == line.VariantId).ProductName} narxi belgilanmagan.",
                            "product_price_missing");
            var code = price.Currency.Trim().ToUpperInvariant();
            var priceRate = await RateAsync(code);
            resolvedPrices[line.VariantId] = new IssuePrice(
                Math.Round(price.SellingPrice * priceRate, 2), code, priceRate);
        }

        var policy = await settings.GetAsync<SalesPolicySettings>(SettingKeys.SalesPolicy, cancellationToken)
                     ?? new SalesPolicySettings();
        await stockAllocator.PreloadAsync(tradeCase.WarehouseId, variantIds, cancellationToken);

        var businessDate = request.BusinessDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var document = new GoodsIssueDocument
        {
            BranchId = tradeCase.BranchId,
            TradeCaseId = tradeCase.Id,
            WarehouseId = tradeCase.WarehouseId,
            CustomerId = tradeCase.CustomerId,
            UserId = userId,
            DocumentNumber = await DocumentNumbers.NextAsync(db, "ISS", businessDate, cancellationToken),
            BusinessDate = businessDate,
            Currency = baseCode,
            Note = NormalizeOptional(request.Note),
            IdempotencyKey = idempotencyKey
        };

        foreach (var input in request.Lines)
        {
            var price = resolvedPrices[input.VariantId];
            var allocations = await stockAllocator.AllocateAsync(tradeCase.WarehouseId,
                input.VariantId, input.Quantity, policy.AllowInsufficientStockSales, cancellationToken);
            foreach (var allocation in allocations)
            {
                document.Lines.Add(new GoodsIssueLine
                {
                    VariantId = input.VariantId,
                    StockId = allocation.Batch.Id,
                    Stock = allocation.Batch,
                    Quantity = allocation.Quantity,
                    UnitPrice = price.AmountBase,
                    PriceCurrency = price.Currency,
                    PriceRate = price.Rate,
                    PurchasePrice = allocation.Batch.PurchasePrice
                });
                allocation.Batch.Quantity -= allocation.Quantity;
                document.EstimatedAmount += Math.Round(allocation.Quantity * price.AmountBase, 2);
            }
        }

        db.GoodsIssueDocuments.Add(document);
        await db.SaveChangesAsync(cancellationToken);

        foreach (var group in document.Lines.GroupBy(x => x.VariantId))
            await db.UpsertInventoryPositionAsync(tradeCase.BranchId,
                InventoryLocationKind.CustomerCustody, tradeCase.Id, group.Key,
                group.Sum(x => x.Quantity), userId, cancellationToken);

        foreach (var line in document.Lines)
            db.InventoryMovements.Add(new InventoryMovement
            {
                BranchId = tradeCase.BranchId,
                VariantId = line.VariantId,
                Quantity = line.Quantity,
                Kind = InventoryMovementKind.GoodsIssue,
                FromLocationKind = InventoryLocationKind.Warehouse,
                FromLocationId = tradeCase.WarehouseId,
                ToLocationKind = InventoryLocationKind.CustomerCustody,
                ToLocationId = tradeCase.Id,
                SourceType = "GoodsIssue",
                SourceId = document.Id,
                UserId = userId
            });

        tradeCase.Version++;
        await branchCatalog.ActivateAsync(tradeCase.BranchId, variantIds,
            BranchCatalogActivationSource.Sale, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        audit.SetOutcome("goods.issued", "goods_issue_documents", document.Id, new
        {
            document.DocumentNumber,
            document.TradeCaseId,
            document.CustomerId,
            document.WarehouseId,
            document.EstimatedAmount,
            lines = document.Lines.Select(x => new { x.VariantId, x.StockId, x.Quantity, x.UnitPrice })
        }, "Mahsulot mijoz saqloviga berildi", tradeCase.BranchId);
        return new GoodsIssueCreatedDto(document.Id, document.DocumentNumber,
            document.EstimatedAmount, tradeCase.Version);
    }

    private static void EnsureEditable(TradeCase tradeCase, int? expectedVersion)
    {
        if (tradeCase.Status is TradeCaseStatus.Settled or TradeCaseStatus.Cancelled)
            throw new BusinessRuleException("Yopilgan loyihaga o'zgartirish kiritib bo'lmaydi.", "trade_case_closed");
        if (expectedVersion.HasValue && expectedVersion != tradeCase.Version)
            throw new ConflictException("Loyiha boshqa qurilmada yangilangan. Ma'lumotni qayta yuklang.", "trade_case_version_conflict");
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class CreateGoodsIssueCommandValidator : AbstractValidator<CreateGoodsIssueCommand>
{
    public CreateGoodsIssueCommandValidator()
    {
        RuleFor(x => x.TradeCaseId).GreaterThan(0);
        RuleFor(x => x.Lines).NotEmpty().Must(x => x.Count <= 500);
        RuleForEach(x => x.Lines).ChildRules(x =>
        {
            x.RuleFor(y => y.VariantId).GreaterThan(0);
            x.RuleFor(y => y.Quantity).GreaterThan(0);
            x.RuleFor(y => y.UnitPrice).GreaterThanOrEqualTo(0).When(y => y.UnitPrice.HasValue);
        });
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.IdempotencyKey).MaximumLength(128);
    }
}
