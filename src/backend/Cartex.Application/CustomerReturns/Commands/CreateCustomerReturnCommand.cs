using Cartex.Application.Common.Documents;
using Cartex.Application.Common.Finance;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Inventory;
using Cartex.Application.Common.Measurement;
using Cartex.Application.Common.Partners;
using Cartex.Application.Common.Settings;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Shared.Models.Sales;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.CustomerReturns.Commands;

public sealed record CustomerReturnLineInput(
    long VariantId,
    decimal Quantity,
    long? SaleItemId,
    decimal? UnitPrice,
    string? Reason,
    ReturnItemCondition Condition,
    InventoryDisposition Disposition);

public sealed record CustomerReturnSettlementInput(
    ReturnSettlementMethod Method,
    string Currency,
    decimal Amount);

internal sealed record ResolvedSettlement(
    ReturnSettlementMethod Method,
    string Currency,
    decimal Amount,
    decimal Rate,
    decimal AmountBase,
    long? SaleId);

internal sealed record ResolvedReturnLine(
    CustomerReturnLineInput Input,
    SaleItem? SaleItem,
    decimal UnitPrice,
    decimal LineAmount);

public sealed record CreateCustomerReturnCommand(
    long WarehouseId,
    List<CustomerReturnLineInput> Lines,
    long? CustomerId = null,
    List<CustomerReturnSettlementInput>? Settlements = null,
    bool AutoSettle = true,
    DateOnly? BusinessDate = null,
    string? Note = null,
    string? IdempotencyKey = null) : ICommand<CustomerReturnCreatedDto>;

public sealed class CreateCustomerReturnCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    ILedgerService ledger,
    ICurrencyService currency,
    ISettingsService settings,
    IQuantityPolicyService quantityPolicy,
    IStockAllocator stockAllocator,
    IPartnerRewardService partnerRewards,
    IAuditService audit) : IRequestHandler<CreateCustomerReturnCommand, CustomerReturnCreatedDto>
{
    public async Task<CustomerReturnCreatedDto> Handle(CreateCustomerReturnCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("Not authenticated.");
        if (!currentUser.HasPermission(AppPermissions.Returns.Create))
            throw new ForbiddenException("Mahsulot qaytaruvini rasmiylashtirishga ruxsat yo'q.");
        if (request.Lines.Any(x => x.SaleItemId is null) && !currentUser.HasPermission(AppPermissions.Returns.FreeLine))
            throw new ForbiddenException("Savdoga bog'lanmagan mahsulotni qaytarishga ruxsat yo'q.");

        var warehouse = await db.Warehouses.AsNoTracking()
            .Where(x => x.Id == request.WarehouseId)
            .Select(x => new { x.Id, x.BranchId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Warehouse not found.", "warehouse_not_found");
        if (!currentUser.CanAccessAllBranches && !currentUser.BranchIds.Contains(warehouse.BranchId))
            throw new NotFoundException("Warehouse not found.", "warehouse_not_found");

        var idempotencyKey = NormalizeOptional(request.IdempotencyKey);
        if (idempotencyKey is not null)
        {
            var existing = await db.CustomerReturnDocuments
                .Where(x => x.BranchId == warehouse.BranchId && x.IdempotencyKey == idempotencyKey)
                .Select(x => new CustomerReturnCreatedDto(x.Id, x.DocumentNumber, x.RefundAmount))
                .FirstOrDefaultAsync(cancellationToken);
            if (existing is not null)
                return existing;
        }

        var saleItemIds = request.Lines.Where(x => x.SaleItemId is not null).Select(x => x.SaleItemId!.Value).ToList();
        if (saleItemIds.Distinct().Count() != saleItemIds.Count)
            throw new BusinessRuleException("Bitta savdo qatori qaytarishda takrorlanmasligi kerak.", "duplicate_return_line");

        var sales = await LoadAndLockSalesAsync(saleItemIds, cancellationToken);
        var saleIds = sales.Keys.ToList();
        var saleItems = saleIds.Count == 0
            ? []
            : await db.SaleItems.Where(x => saleIds.Contains(x.SaleId)).ToListAsync(cancellationToken);
        var itemById = saleItems.ToDictionary(x => x.Id);

        foreach (var line in request.Lines.Where(x => x.SaleItemId is not null))
        {
            if (!itemById.TryGetValue(line.SaleItemId!.Value, out var item))
                throw new NotFoundException("Sale item not found.", "sale_item_not_found");
            var sale = sales[item.SaleId];
            if (!currentUser.CanAccessAllBranches && !currentUser.BranchIds.Contains(sale.BranchId))
                throw new NotFoundException("Sale not found.", "sale_not_found");
            if (request.CustomerId is { } customerId && sale.CustomerId != customerId)
                throw new BusinessRuleException("Tanlangan savdo boshqa mijozga tegishli.", "sale_customer_mismatch");
            if (line.Quantity > item.Quantity - item.ReturnedQuantity)
                throw new BusinessRuleException("Qaytariladigan miqdor qolgan miqdordan oshib ketdi.", "return_quantity_exceeded");
            if (line.VariantId != item.VariantId)
                throw new BusinessRuleException("Qaytarish qatori savdo qatoriga mos emas.", "return_line_variant_mismatch");
        }

        await quantityPolicy.ValidateAsync(
            request.Lines.Select(x => (x.VariantId, x.Quantity)), cancellationToken);

        // The discount already sits on the line, so a refund is simply the part of that line's net
        // this return consumes. Taking the difference keeps repeated partial returns adding up
        // to the net exactly, with no cent stranded on the last one.
        static decimal NetConsumed(SaleItem item, decimal quantity) =>
            item.Quantity <= 0
                ? 0
                : Math.Round((item.Quantity * item.UnitPrice - item.DiscountAmount) * quantity / item.Quantity, 2);

        var resolved = new List<ResolvedReturnLine>(request.Lines.Count);
        foreach (var input in request.Lines)
        {
            if (input.SaleItemId is { } saleItemId)
            {
                var item = itemById[saleItemId];
                resolved.Add(new ResolvedReturnLine(input, item, item.UnitPrice,
                    NetConsumed(item, item.ReturnedQuantity + input.Quantity) - NetConsumed(item, item.ReturnedQuantity)));
            }
            else
            {
                var unitPrice = input.UnitPrice
                    ?? throw new BusinessRuleException("Savdoga bog'lanmagan qator uchun narx kerak.", "return_line_price_required");
                resolved.Add(new ResolvedReturnLine(input, null, unitPrice,
                    Math.Round(input.Quantity * unitPrice, 2)));
            }
        }

        var grossAmount = resolved.Sum(x => x.Input.Quantity * x.UnitPrice);
        var refundAmount = resolved.Sum(x => x.LineAmount);
        // A fully discounted line refunds nothing, but the goods still have to come back.
        if (refundAmount < 0)
            throw new BusinessRuleException("Qaytaruv qiymati manfiy bo'lishi mumkin emas.", "return_value_negative");

        var businessDate = request.BusinessDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var document = new CustomerReturnDocument
        {
            BranchId = warehouse.BranchId,
            WarehouseId = warehouse.Id,
            CustomerId = request.CustomerId,
            UserId = userId,
            DocumentNumber = await DocumentNumbers.NextAsync(db, "RET", businessDate, cancellationToken),
            BusinessDate = businessDate,
            GrossAmount = grossAmount,
            RefundAmount = refundAmount,
            Note = NormalizeOptional(request.Note),
            IdempotencyKey = idempotencyKey
        };

        foreach (var line in resolved)
        {
            var item = line.SaleItem;
            var cashback = item is null ? 0 : CashbackFor(item, line.Input.Quantity);
            document.Lines.Add(new CustomerReturnLine
            {
                SaleId = item?.SaleId,
                SaleItemId = item?.Id,
                VariantId = line.Input.VariantId,
                StockId = item?.StockId,
                Quantity = line.Input.Quantity,
                UnitPrice = line.UnitPrice,
                PriceCurrency = item?.PriceCurrency ?? await currency.BaseAsync(cancellationToken),
                PriceRate = item?.PriceRate ?? 1m,
                LineAmount = line.LineAmount,
                CashbackReversed = cashback,
                Reason = NormalizeOptional(line.Input.Reason),
                Condition = line.Input.Condition,
                Disposition = line.Input.Disposition
            });
        }

        db.CustomerReturnDocuments.Add(document);
        await db.SaveChangesAsync(cancellationToken);

        foreach (var line in document.Lines.Where(x => x.SaleItemId is not null))
        {
            var item = itemById[line.SaleItemId!.Value];
            item.ReturnedQuantity += line.Quantity;
            item.ReturnedCashback += line.CashbackReversed;
        }

        await ApplyInventoryAsync(document, userId, cancellationToken);

        var resolvedSettlements = request.Settlements is { Count: > 0 }
            ? await ResolveExplicitSettlementsAsync(request.Settlements, refundAmount, cancellationToken)
            : request.AutoSettle
                ? await BuildAutomaticSettlementsAsync(document, sales, cancellationToken)
                : throw new BusinessRuleException("Qaytaruv hisob-kitobi tanlanishi kerak.", "return_settlement_required");

        await ApplySettlementsAsync(document, sales, resolvedSettlements, userId, cancellationToken);
        await ReverseCashbackAsync(document, userId, cancellationToken);
        await partnerRewards.ReverseReturnAsync(document, cancellationToken);

        foreach (var sale in sales.Values)
        {
            var items = saleItems.Where(x => x.SaleId == sale.Id).ToList();
            sale.Status = items.All(x => x.ReturnedQuantity >= x.Quantity)
                ? SaleStatus.Returned
                : items.Any(x => x.ReturnedQuantity > 0) ? SaleStatus.PartialReturn : SaleStatus.Completed;
        }
        await db.SaveChangesAsync(cancellationToken);

        audit.SetOutcome("goods.returned", "customer_return_documents", document.Id, new
        {
            document.DocumentNumber,
            document.CustomerId,
            document.WarehouseId,
            document.GrossAmount,
            document.RefundAmount,
            document.CashbackReversed,
            sales = sales.Keys,
            lines = document.Lines.Select(x => new
            {
                x.SaleId,
                x.SaleItemId,
                x.VariantId,
                x.Quantity,
                x.UnitPrice,
                x.LineAmount,
                IsFreeLine = x.SaleItemId is null,
                x.Reason,
                x.Condition,
                x.Disposition
            }),
            settlements = document.Settlements.Select(x => new { x.Method, x.Currency, x.Amount, x.Rate, x.AmountBase })
        }, "Mahsulot qaytaruvi rasmiylashtirildi", document.BranchId);

        return new CustomerReturnCreatedDto(document.Id, document.DocumentNumber, document.RefundAmount);
    }

    // Locks every source sale in a stable id order so concurrent partial returns cannot deadlock.
    private async Task<Dictionary<long, Sale>> LoadAndLockSalesAsync(
        IReadOnlyCollection<long> saleItemIds,
        CancellationToken cancellationToken)
    {
        if (saleItemIds.Count == 0) return [];
        var saleIds = await db.SaleItems
            .Where(x => saleItemIds.Contains(x.Id))
            .Select(x => x.SaleId)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);
        var idArray = saleIds.ToArray();
        var sales = await db.Sales
            .FromSqlInterpolated($"SELECT * FROM sales WHERE id = ANY({idArray}) ORDER BY id FOR UPDATE")
            .ToListAsync(cancellationToken);
        if (sales.Count != saleIds.Count)
            throw new NotFoundException("Sale not found.", "sale_not_found");
        return sales.ToDictionary(x => x.Id);
    }

    private static decimal CashbackFor(SaleItem item, decimal quantity)
    {
        var remaining = Math.Max(0, item.CashbackEarned - item.ReturnedCashback);
        return quantity >= item.Quantity - item.ReturnedQuantity
            ? remaining
            : Math.Min(remaining, Math.Round(item.CashbackEarned * quantity / item.Quantity, 2));
    }

    private async Task ApplyInventoryAsync(
        CustomerReturnDocument document,
        long userId,
        CancellationToken cancellationToken)
    {
        var restockLines = document.Lines
            .Where(x => x.Disposition == InventoryDisposition.SellableRestock)
            .ToList();
        if (restockLines.Count > 0)
        {
            var knownIds = restockLines.Where(x => x.StockId is not null)
                .Select(x => x.StockId!.Value).Distinct().ToList();
            var stocks = knownIds.Count == 0
                ? []
                : await db.Stocks.Where(x => knownIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
            foreach (var line in restockLines)
            {
                Stock batch;
                if (line.StockId is { } stockId && stocks.TryGetValue(stockId, out var known))
                {
                    batch = known;
                }
                else
                {
                    batch = await stockAllocator.ResolveRestockBatchAsync(
                        document.WarehouseId, line.VariantId, cancellationToken);
                    line.Stock = batch;
                }
                batch.Quantity += line.Quantity;
            }
        }

        foreach (var group in document.Lines
                     .Where(x => x.Disposition != InventoryDisposition.SellableRestock)
                     .GroupBy(x => new { x.VariantId, x.Disposition }))
        {
            await db.UpsertInventoryPositionAsync(
                document.BranchId,
                LocationFor(group.Key.Disposition),
                document.WarehouseId,
                group.Key.VariantId,
                group.Sum(x => x.Quantity),
                userId,
                cancellationToken);
        }

        foreach (var line in document.Lines)
        {
            db.InventoryMovements.Add(new InventoryMovement
            {
                BranchId = document.BranchId,
                VariantId = line.VariantId,
                Quantity = line.Quantity,
                Kind = InventoryMovementKind.SaleReturn,
                FromLocationKind = document.CustomerId.HasValue ? InventoryLocationKind.Customer : InventoryLocationKind.External,
                FromLocationId = document.CustomerId ?? 0,
                ToLocationKind = line.Disposition == InventoryDisposition.SellableRestock
                    ? InventoryLocationKind.Warehouse
                    : LocationFor(line.Disposition),
                ToLocationId = document.WarehouseId,
                SourceType = "CustomerReturn",
                SourceId = document.Id,
                UserId = userId,
                OccurredAt = DateTime.UtcNow
            });
        }
    }

    private async Task<List<ResolvedSettlement>> ResolveExplicitSettlementsAsync(
        IReadOnlyCollection<CustomerReturnSettlementInput> rows,
        decimal refundAmount,
        CancellationToken cancellationToken)
    {
        var baseCode = await currency.BaseAsync(cancellationToken);
        var result = new List<ResolvedSettlement>();
        foreach (var row in rows)
        {
            var code = string.IsNullOrWhiteSpace(row.Currency) ? baseCode : row.Currency.Trim().ToUpperInvariant();
            await currency.EnsureSalesAllowedAsync(code, cancellationToken);
            var rate = await currency.RateAsync(code, cancellationToken);
            result.Add(new ResolvedSettlement(row.Method, code, row.Amount, rate, Math.Round(row.Amount * rate, 2), null));
        }
        if (result.Sum(x => x.AmountBase) != refundAmount)
            throw new BusinessRuleException("Qaytaruv hisob-kitobi summasi qaytaruv qiymatiga teng emas.", "return_settlement_mismatch");
        return result;
    }

    // Each source sale is settled against its own remaining payment capacity, then the
    // leftover (and every free line) falls back to the customer's debt and advance.
    private async Task<List<ResolvedSettlement>> BuildAutomaticSettlementsAsync(
        CustomerReturnDocument document,
        IReadOnlyDictionary<long, Sale> sales,
        CancellationToken cancellationToken)
    {
        var baseCode = await currency.BaseAsync(cancellationToken);
        var result = new List<ResolvedSettlement>();
        var unallocated = 0m;

        foreach (var group in document.Lines.GroupBy(x => x.SaleId))
        {
            var amount = group.Sum(x => x.LineAmount);
            if (group.Key is not { } saleId)
            {
                unallocated += amount;
                continue;
            }

            var sale = sales[saleId];
            var remaining = amount;

            void Take(ReturnSettlementMethod method, decimal availableBase)
            {
                var take = Math.Min(remaining, Math.Max(0, availableBase));
                if (take <= 0) return;
                result.Add(new ResolvedSettlement(method, baseCode, take, 1m, take, saleId));
                remaining -= take;
            }

            if (sale.CustomerId is { } saleCustomerId && remaining > 0)
            {
                var debt = await ledger.FindCustomerAccountAsync(saleCustomerId, AccountType.Debt, cancellationToken, sale.DebtCurrency);
                var debtRate = sale.DebtRate == 0 ? 1m : sale.DebtRate;
                var availableDebtBase = Math.Min(
                    Math.Max(0, sale.DebtAmount - sale.RefundedDebt),
                    Math.Max(0, (debt?.Balance ?? 0) * debtRate));
                var debtBase = Math.Min(remaining, availableDebtBase);
                if (debtBase > 0)
                {
                    var native = Math.Round(debtBase / debtRate, 4);
                    var actualBase = Math.Round(native * debtRate, 2);
                    result.Add(new ResolvedSettlement(ReturnSettlementMethod.ReduceDebt,
                        sale.DebtCurrency, native, debtRate, actualBase, saleId));
                    remaining -= actualBase;
                }
            }

            Take(ReturnSettlementMethod.Bonus, sale.PaidBonus - sale.RefundedBonus);
            Take(ReturnSettlementMethod.Card, sale.PaidCard - sale.RefundedCard);
            Take(ReturnSettlementMethod.Cash, sale.PaidCash - sale.RefundedCash);
            unallocated += remaining;
        }

        if (unallocated <= 0)
            return result;

        if (document.CustomerId is not { } customerId)
            throw new BusinessRuleException(
                "Mijozsiz qaytaruvda hisob-kitob usuli ko'rsatilishi kerak.", "return_settlement_required");

        var customerDebt = await ledger.FindCustomerAccountAsync(customerId, AccountType.Debt, cancellationToken, baseCode);
        var reduceDebt = Math.Min(unallocated, Math.Max(0, customerDebt?.Balance ?? 0));
        if (reduceDebt > 0)
        {
            result.Add(new ResolvedSettlement(ReturnSettlementMethod.ReduceDebt, baseCode, reduceDebt, 1m, reduceDebt, null));
            unallocated -= reduceDebt;
        }
        if (unallocated > 0)
            result.Add(new ResolvedSettlement(ReturnSettlementMethod.CustomerAdvance, baseCode, unallocated, 1m, unallocated, null));
        return result;
    }

    private async Task ApplySettlementsAsync(
        CustomerReturnDocument document,
        IReadOnlyDictionary<long, Sale> sales,
        IReadOnlyCollection<ResolvedSettlement> settlements,
        long userId,
        CancellationToken cancellationToken)
    {
        var shiftId = await db.Shifts
            .Where(x => x.UserId == userId && x.BranchId == document.BranchId && x.Status == ShiftStatus.Open)
            .Select(x => (long?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        var policy = await settings.GetAsync<SalesPolicySettings>(SettingKeys.SalesPolicy, cancellationToken)
            ?? new SalesPolicySettings();
        if (settlements.Any(x => x.Method == ReturnSettlementMethod.Cash) && shiftId is null && policy.ShiftPolicy != "Off")
            throw new BusinessRuleException("Naqd qaytarish uchun ochiq smena talab qilinadi.");

        foreach (var settlement in settlements)
        {
            document.Settlements.Add(new CustomerReturnSettlement
            {
                Method = settlement.Method,
                Currency = settlement.Currency,
                Amount = settlement.Amount,
                Rate = settlement.Rate,
                AmountBase = settlement.AmountBase
            });

            var sale = settlement.SaleId is { } saleId ? sales[saleId] : null;
            Transaction? transaction = null;
            switch (settlement.Method)
            {
                case ReturnSettlementMethod.ReduceDebt:
                {
                    var customerId = sale?.CustomerId ?? document.CustomerId
                        ?? throw new BusinessRuleException("Qarzni kamaytirish uchun mijoz kerak.");
                    var debt = await ledger.FindCustomerAccountAsync(customerId, AccountType.Debt, cancellationToken, settlement.Currency)
                        ?? throw new BusinessRuleException("Mijoz qarzi mavjud emas.");
                    if (settlement.Amount > debt.Balance)
                        throw new BusinessRuleException("Qaytaruv mijozning mavjud qarzidan oshib ketdi.");
                    transaction = ledger.Post(OperationType.SaleReturn, settlement.Amount, debt, null, userId, shiftId, settlement.Rate);
                    if (sale is not null) sale.RefundedDebt += settlement.AmountBase;
                    break;
                }
                case ReturnSettlementMethod.Cash:
                case ReturnSettlementMethod.Card:
                {
                    var accountType = settlement.Method == ReturnSettlementMethod.Cash ? AccountType.Cash : AccountType.Card;
                    var account = await ledger.BranchAccountAsync(document.BranchId, accountType, cancellationToken, settlement.Currency);
                    if (account.Balance < settlement.Amount)
                        throw new BusinessRuleException("Qaytaruv uchun kassadagi mablag' yetarli emas.", "insufficient_refund_funds");
                    transaction = ledger.Post(OperationType.CustomerRefund, settlement.Amount, account, null, userId, shiftId, settlement.Rate);
                    if (sale is not null)
                    {
                        if (settlement.Method == ReturnSettlementMethod.Cash) sale.RefundedCash += settlement.AmountBase;
                        else sale.RefundedCard += settlement.AmountBase;
                    }
                    break;
                }
                case ReturnSettlementMethod.Bonus:
                {
                    var customerId = sale?.CustomerId ?? document.CustomerId
                        ?? throw new BusinessRuleException("Bonusga qaytarish uchun mijoz kerak.");
                    var bonus = await ledger.CustomerAccountAsync(customerId, AccountType.Bonus, cancellationToken, settlement.Currency);
                    transaction = ledger.Post(OperationType.SaleReturn, settlement.Amount, null, bonus, userId, shiftId, settlement.Rate);
                    if (sale is not null) sale.RefundedBonus += settlement.AmountBase;
                    break;
                }
                case ReturnSettlementMethod.CustomerAdvance:
                {
                    var customerId = sale?.CustomerId ?? document.CustomerId
                        ?? throw new BusinessRuleException("Avansga qaytarish uchun mijoz kerak.");
                    var advance = await ledger.CustomerAccountAsync(customerId, AccountType.CustomerAdvance, cancellationToken, settlement.Currency);
                    transaction = ledger.Post(OperationType.CustomerAdvance, settlement.Amount, null, advance, userId, shiftId, settlement.Rate);
                    if (sale is not null) sale.RefundedAdvance += settlement.AmountBase;
                    break;
                }
                case ReturnSettlementMethod.NoCharge:
                    if (sale is not null) sale.ReturnNoChargeAmount += settlement.AmountBase;
                    break;
                default:
                    throw new BusinessRuleException("Qaytaruv hisob-kitobi turi qo'llab-quvvatlanmaydi.");
            }

            if (transaction is not null)
            {
                transaction.CustomerReturnDocument = document;
                transaction.SaleId = settlement.SaleId;
                transaction.Description = document.DocumentNumber;
            }
        }
    }

    private async Task ReverseCashbackAsync(
        CustomerReturnDocument document,
        long userId,
        CancellationToken cancellationToken)
    {
        var amount = document.Lines.Sum(x => x.CashbackReversed);
        if (amount <= 0 || document.CustomerId is not { } customerId) return;

        var bonus = await ledger.CustomerAccountAsync(customerId, AccountType.Bonus, cancellationToken);
        var fromBonus = Math.Min(amount, Math.Max(0, bonus.Balance));
        if (fromBonus > 0)
        {
            var transaction = ledger.Post(OperationType.Cashback, fromBonus, bonus, null, userId);
            transaction.CustomerReturnDocument = document;
            transaction.Description = document.DocumentNumber;
        }

        var recoveryAmount = amount - fromBonus;
        if (recoveryAmount > 0)
        {
            var recovery = await ledger.CustomerAccountAsync(customerId, AccountType.RewardRecovery, cancellationToken);
            var transaction = ledger.Post(OperationType.CashbackRecovery, recoveryAmount, null, recovery, userId);
            transaction.CustomerReturnDocument = document;
            transaction.Description = document.DocumentNumber;
        }

        document.CashbackReversed = amount;
    }

    private static InventoryLocationKind LocationFor(InventoryDisposition disposition) => disposition switch
    {
        InventoryDisposition.Quarantine => InventoryLocationKind.Quarantine,
        InventoryDisposition.Scrap => InventoryLocationKind.Scrap,
        InventoryDisposition.SupplierClaim => InventoryLocationKind.SupplierClaim,
        _ => InventoryLocationKind.Warehouse
    };

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class CreateCustomerReturnCommandValidator : AbstractValidator<CreateCustomerReturnCommand>
{
    public CreateCustomerReturnCommandValidator()
    {
        RuleFor(x => x.WarehouseId).GreaterThan(0);
        RuleFor(x => x.CustomerId).GreaterThan(0).When(x => x.CustomerId.HasValue);
        RuleFor(x => x.Lines).NotEmpty().Must(x => x.Count <= 500);
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(x => x.VariantId).GreaterThan(0);
            line.RuleFor(x => x.Quantity).GreaterThan(0);
            line.RuleFor(x => x.SaleItemId).GreaterThan(0).When(x => x.SaleItemId.HasValue);
            line.RuleFor(x => x.UnitPrice).NotNull().GreaterThan(0).When(x => x.SaleItemId is null);
            line.RuleFor(x => x.Reason).MaximumLength(500);
        });
        RuleFor(x => x.Settlements).Must(x => x is null || x.Count <= 20);
        RuleForEach(x => x.Settlements!).ChildRules(row =>
        {
            row.RuleFor(x => x.Amount).GreaterThan(0);
            row.RuleFor(x => x.Currency).MaximumLength(3);
        }).When(x => x.Settlements is not null);
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.IdempotencyKey).MaximumLength(128);
    }
}
