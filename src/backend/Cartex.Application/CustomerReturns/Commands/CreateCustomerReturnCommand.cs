using Cartex.Application.Common.Documents;
using Cartex.Application.Common.Finance;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Measurement;
using Cartex.Application.Common.Settings;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Shared.Models.Sales;
using Cartex.Application.Common.Partners;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.CustomerReturns.Commands;

public sealed record CustomerReturnLineInput(
    long SaleItemId,
    decimal Quantity,
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
    decimal AmountBase);

public sealed record CreateCustomerReturnCommand(
    long SaleId,
    List<CustomerReturnLineInput> Lines,
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
    IPartnerRewardService partnerRewards,
    IAuditService audit) : IRequestHandler<CreateCustomerReturnCommand, CustomerReturnCreatedDto>
{
    public async Task<CustomerReturnCreatedDto> Handle(CreateCustomerReturnCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("Not authenticated.");
        if (!currentUser.HasPermission(AppPermissions.Returns.Create)
            && !currentUser.HasPermission(AppPermissions.Sales.Return))
            throw new ForbiddenException("Mahsulot qaytaruvini rasmiylashtirishga ruxsat yo'q.");

        // Serializes all partial returns for the same sale and prevents over-return races.
        var sale = await db.Sales
            .FromSqlInterpolated($"SELECT * FROM sales WHERE id = {request.SaleId} FOR UPDATE")
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Sale not found.", "sale_not_found");
        if (!currentUser.CanAccessAllBranches && !currentUser.BranchIds.Contains(sale.BranchId))
            throw new NotFoundException("Sale not found.", "sale_not_found");

        var idempotencyKey = NormalizeOptional(request.IdempotencyKey);
        if (idempotencyKey is not null)
        {
            var existing = await db.CustomerReturnDocuments
                .Where(x => x.BranchId == sale.BranchId && x.IdempotencyKey == idempotencyKey)
                .Select(x => new CustomerReturnCreatedDto(x.Id, x.DocumentNumber, x.RefundAmount, x.IsFullReturn))
                .FirstOrDefaultAsync(cancellationToken);
            if (existing is not null)
                return existing;
        }

        if (sale.Status == SaleStatus.Returned)
            throw new BusinessRuleException("Savdo allaqachon to'liq qaytarilgan.", "sale_already_returned");
        if (request.Lines.Select(x => x.SaleItemId).Distinct().Count() != request.Lines.Count)
            throw new BusinessRuleException("Bitta savdo qatori qaytarishda takrorlanmasligi kerak.", "duplicate_return_line");

        var saleItems = await db.SaleItems.Where(x => x.SaleId == sale.Id).ToListAsync(cancellationToken);
        var itemById = saleItems.ToDictionary(x => x.Id);
        foreach (var line in request.Lines)
        {
            if (!itemById.TryGetValue(line.SaleItemId, out var item))
                throw new NotFoundException("Sale item not found.", "sale_item_not_found");
            if (line.Quantity > item.Quantity - item.ReturnedQuantity)
                throw new BusinessRuleException("Qaytariladigan miqdor qolgan miqdordan oshib ketdi.", "return_quantity_exceeded");
        }

        await quantityPolicy.ValidateAsync(request.Lines.Select(x =>
        {
            var item = itemById[x.SaleItemId];
            return (item.VariantId, x.Quantity);
        }), cancellationToken);

        var requestedByItem = request.Lines.ToDictionary(x => x.SaleItemId, x => x.Quantity);
        var isFullReturn = saleItems.All(x =>
            x.ReturnedQuantity + requestedByItem.GetValueOrDefault(x.Id) >= x.Quantity);
        var saleGross = saleItems.Sum(x => x.Quantity * x.UnitPrice);
        var grossAmount = request.Lines.Sum(x => x.Quantity * itemById[x.SaleItemId].UnitPrice);
        var discountRate = saleGross > 0 ? sale.DiscountAmount / saleGross : 0;
        var alreadySettled = sale.RefundedCash + sale.RefundedCard + sale.RefundedBonus + sale.RefundedDebt
            + sale.RefundedAdvance + sale.ReturnNoChargeAmount;
        var refundAmount = isFullReturn
            ? Math.Max(0, sale.TotalAmount - alreadySettled)
            : Math.Round(grossAmount * (1 - discountRate), 2);
        if (refundAmount <= 0)
            throw new BusinessRuleException("Qaytaruv uchun moliyaviy qoldiq mavjud emas.", "return_value_exhausted");

        var businessDate = request.BusinessDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var document = new CustomerReturnDocument
        {
            BranchId = sale.BranchId,
            WarehouseId = sale.WarehouseId,
            CustomerId = sale.CustomerId,
            SaleId = sale.Id,
            UserId = userId,
            DocumentNumber = await DocumentNumbers.NextAsync(db, "RET", businessDate, cancellationToken),
            BusinessDate = businessDate,
            GrossAmount = grossAmount,
            RefundAmount = refundAmount,
            IsFullReturn = isFullReturn,
            Note = NormalizeOptional(request.Note),
            IdempotencyKey = idempotencyKey
        };

        decimal lineAmountUsed = 0;
        for (var index = 0; index < request.Lines.Count; index++)
        {
            var input = request.Lines[index];
            var item = itemById[input.SaleItemId];
            var lineAmount = index == request.Lines.Count - 1
                ? refundAmount - lineAmountUsed
                : Math.Round(input.Quantity * item.UnitPrice * (1 - discountRate), 2);
            lineAmountUsed += lineAmount;
            var remainingCashback = Math.Max(0, item.CashbackEarned - item.ReturnedCashback);
            var cashback = input.Quantity == item.Quantity - item.ReturnedQuantity
                ? remainingCashback
                : Math.Min(remainingCashback, Math.Round(item.CashbackEarned * input.Quantity / item.Quantity, 2));

            document.Lines.Add(new CustomerReturnLine
            {
                SaleItemId = item.Id,
                VariantId = item.VariantId,
                StockId = item.StockId,
                Quantity = input.Quantity,
                UnitPrice = item.UnitPrice,
                PriceCurrency = item.PriceCurrency,
                PriceRate = item.PriceRate,
                LineAmount = lineAmount,
                CashbackReversed = cashback,
                Reason = NormalizeOptional(input.Reason),
                Condition = input.Condition,
                Disposition = input.Disposition
            });
        }

        db.CustomerReturnDocuments.Add(document);
        await db.SaveChangesAsync(cancellationToken);

        foreach (var line in document.Lines)
        {
            var item = itemById[line.SaleItemId];
            item.ReturnedQuantity += line.Quantity;
            item.ReturnedCashback += line.CashbackReversed;
        }

        await ApplyInventoryAsync(document, userId, cancellationToken);

        var resolvedSettlements = request.Settlements is { Count: > 0 }
            ? await ResolveExplicitSettlementsAsync(request.Settlements, refundAmount, cancellationToken)
            : request.AutoSettle
                ? await BuildAutomaticSettlementsAsync(sale, refundAmount, cancellationToken)
                : throw new BusinessRuleException("Qaytaruv hisob-kitobi tanlanishi kerak.", "return_settlement_required");

        await ApplySettlementsAsync(document, sale, resolvedSettlements, userId, cancellationToken);
        await ReverseCashbackAsync(document, sale, userId, cancellationToken);
        await partnerRewards.ReverseReturnAsync(document, cancellationToken);

        sale.Status = isFullReturn ? SaleStatus.Returned : SaleStatus.PartialReturn;
        await db.SaveChangesAsync(cancellationToken);

        audit.SetOutcome("goods.returned", "customer_return_documents", document.Id, new
        {
            document.DocumentNumber,
            document.SaleId,
            document.CustomerId,
            document.WarehouseId,
            document.GrossAmount,
            document.RefundAmount,
            document.CashbackReversed,
            document.IsFullReturn,
            lines = document.Lines.Select(x => new
            {
                x.SaleItemId,
                x.VariantId,
                x.Quantity,
                x.LineAmount,
                x.Reason,
                x.Condition,
                x.Disposition
            }),
            settlements = document.Settlements.Select(x => new { x.Method, x.Currency, x.Amount, x.Rate, x.AmountBase })
        }, "Mahsulot qaytaruvi rasmiylashtirildi", document.BranchId);

        return new CustomerReturnCreatedDto(document.Id, document.DocumentNumber, document.RefundAmount, document.IsFullReturn);
    }

    private async Task ApplyInventoryAsync(
        CustomerReturnDocument document,
        long userId,
        CancellationToken cancellationToken)
    {
        var sellableByStock = document.Lines
            .Where(x => x.Disposition == InventoryDisposition.SellableRestock)
            .GroupBy(x => x.StockId)
            .ToDictionary(x => x.Key, x => x.Sum(line => line.Quantity));
        if (sellableByStock.Count > 0)
        {
            var stockIds = sellableByStock.Keys.ToList();
            var stocks = await db.Stocks.Where(x => stockIds.Contains(x.Id)).ToListAsync(cancellationToken);
            foreach (var stock in stocks)
                stock.Quantity += sellableByStock[stock.Id];
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
                FromLocationId = document.CustomerId ?? document.SaleId,
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
            result.Add(new ResolvedSettlement(row.Method, code, row.Amount, rate, Math.Round(row.Amount * rate, 2)));
        }
        if (result.Sum(x => x.AmountBase) != refundAmount)
            throw new BusinessRuleException("Qaytaruv hisob-kitobi summasi qaytaruv qiymatiga teng emas.", "return_settlement_mismatch");
        return result;
    }

    private async Task<List<ResolvedSettlement>> BuildAutomaticSettlementsAsync(
        Sale sale,
        decimal refundAmount,
        CancellationToken cancellationToken)
    {
        var baseCode = await currency.BaseAsync(cancellationToken);
        var result = new List<ResolvedSettlement>();
        var remaining = refundAmount;

        void Take(ReturnSettlementMethod method, decimal availableBase)
        {
            var amount = Math.Min(remaining, Math.Max(0, availableBase));
            if (amount <= 0) return;
            result.Add(new ResolvedSettlement(method, baseCode, amount, 1m, amount));
            remaining -= amount;
        }

        if (sale.CustomerId is { } customerId && remaining > 0)
        {
            var debt = await ledger.FindCustomerAccountAsync(customerId, AccountType.Debt, cancellationToken, sale.DebtCurrency);
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
                    sale.DebtCurrency, native, debtRate, actualBase));
                remaining -= actualBase;
            }
        }

        Take(ReturnSettlementMethod.Bonus, sale.PaidBonus - sale.RefundedBonus);
        Take(ReturnSettlementMethod.Card, sale.PaidCard - sale.RefundedCard);
        Take(ReturnSettlementMethod.Cash, sale.PaidCash - sale.RefundedCash);

        if (remaining > 0 && sale.CustomerId.HasValue)
            Take(ReturnSettlementMethod.CustomerAdvance, remaining);
        if (remaining > 0)
            throw new BusinessRuleException("Qaytaruvni yopish uchun to'lov manbasi yetarli emas.", "return_settlement_unresolved");
        return result;
    }

    private async Task ApplySettlementsAsync(
        CustomerReturnDocument document,
        Sale sale,
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

            Transaction? transaction = null;
            switch (settlement.Method)
            {
                case ReturnSettlementMethod.ReduceDebt:
                {
                    var customerId = document.CustomerId
                        ?? throw new BusinessRuleException("Qarzni kamaytirish uchun mijoz kerak.");
                    var debt = await ledger.FindCustomerAccountAsync(customerId, AccountType.Debt, cancellationToken, settlement.Currency)
                        ?? throw new BusinessRuleException("Mijoz qarzi mavjud emas.");
                    if (settlement.Amount > debt.Balance)
                        throw new BusinessRuleException("Qaytaruv mijozning mavjud qarzidan oshib ketdi.");
                    transaction = ledger.Post(OperationType.SaleReturn, settlement.Amount, debt, null, userId, shiftId, settlement.Rate);
                    sale.RefundedDebt += settlement.AmountBase;
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
                    if (settlement.Method == ReturnSettlementMethod.Cash) sale.RefundedCash += settlement.AmountBase;
                    else sale.RefundedCard += settlement.AmountBase;
                    break;
                }
                case ReturnSettlementMethod.Bonus:
                {
                    var customerId = document.CustomerId
                        ?? throw new BusinessRuleException("Bonusga qaytarish uchun mijoz kerak.");
                    var bonus = await ledger.CustomerAccountAsync(customerId, AccountType.Bonus, cancellationToken, settlement.Currency);
                    transaction = ledger.Post(OperationType.SaleReturn, settlement.Amount, null, bonus, userId, shiftId, settlement.Rate);
                    sale.RefundedBonus += settlement.AmountBase;
                    break;
                }
                case ReturnSettlementMethod.CustomerAdvance:
                {
                    var customerId = document.CustomerId
                        ?? throw new BusinessRuleException("Avansga qaytarish uchun mijoz kerak.");
                    var advance = await ledger.CustomerAccountAsync(customerId, AccountType.CustomerAdvance, cancellationToken, settlement.Currency);
                    transaction = ledger.Post(OperationType.CustomerAdvance, settlement.Amount, null, advance, userId, shiftId, settlement.Rate);
                    sale.RefundedAdvance += settlement.AmountBase;
                    break;
                }
                case ReturnSettlementMethod.NoCharge:
                    sale.ReturnNoChargeAmount += settlement.AmountBase;
                    break;
                default:
                    throw new BusinessRuleException("Qaytaruv hisob-kitobi turi qo'llab-quvvatlanmaydi.");
            }

            if (transaction is not null)
            {
                transaction.CustomerReturnDocument = document;
                transaction.SaleId = sale.Id;
                transaction.Description = document.DocumentNumber;
            }
        }
    }

    private async Task ReverseCashbackAsync(
        CustomerReturnDocument document,
        Sale sale,
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
            transaction.SaleId = sale.Id;
            transaction.Description = document.DocumentNumber;
        }

        var recoveryAmount = amount - fromBonus;
        if (recoveryAmount > 0)
        {
            var recovery = await ledger.CustomerAccountAsync(customerId, AccountType.RewardRecovery, cancellationToken);
            var transaction = ledger.Post(OperationType.CashbackRecovery, recoveryAmount, null, recovery, userId);
            transaction.CustomerReturnDocument = document;
            transaction.SaleId = sale.Id;
            transaction.Description = document.DocumentNumber;
        }

        document.CashbackReversed = amount;
        sale.RefundedCashback += amount;
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
        RuleFor(x => x.SaleId).GreaterThan(0);
        RuleFor(x => x.Lines).NotEmpty().Must(x => x.Count <= 500);
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(x => x.SaleItemId).GreaterThan(0);
            line.RuleFor(x => x.Quantity).GreaterThan(0);
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
