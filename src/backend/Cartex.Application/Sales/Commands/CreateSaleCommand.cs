using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Domain.Events;
using Cartex.Domain.Authorization;
using Cartex.Application.Common.Finance;
using Cartex.Application.Common.Measurement;
using Cartex.Application.Common.Inventory;
using Cartex.Application.Common.Loyalty;
using Cartex.Application.Common.Participants;
using Cartex.Application.Common.Partners;
using System.Text.Json.Serialization;
using Cartex.Application.Printing;
using Cartex.Application.OfflineCache;

namespace Cartex.Application.Sales.Commands;

public record CreateSaleItemDto(
    long VariantId,
    decimal Quantity,
    decimal? UnitPrice = null,
    long? PrepackId = null,
    [property: JsonIgnore] long? StockId = null,
    [property: JsonIgnore] string? SourceCurrency = null,
    [property: JsonIgnore] decimal? SourceRate = null);

public record SalePaymentDto(PaymentMethod Method, string Currency, decimal Amount);

public record CreateSaleResult(long SaleId, string ReceiptToken);

file sealed record CatalogPrice(ProductPrice Source, decimal Amount, string Currency, decimal Rate);

file sealed record ResolvedSaleLine(
    CreateSaleItemDto Item,
    decimal Quantity,
    decimal UnitPrice,
    string Currency,
    decimal Rate,
    decimal PriceDiscount);

internal sealed record AdvanceUse(Account Account, decimal Amount, decimal Rate, decimal AmountBase);

public record CreateSaleCommand(
    long WarehouseId,
    long? CustomerId,
    decimal PaidCash,
    decimal PaidCard,
    decimal PaidBonus,
    List<CreateSaleItemDto> Items,
    decimal DiscountAmount = 0,
    List<SalePaymentDto>? Payments = null,
    string? DebtCurrency = null,
    DateOnly? DebtDueDate = null,
    string? IdempotencyKey = null,
    bool ApplyAutoDiscount = true,
    decimal CreditAmount = 0,
    bool FromQueuedCart = false,
    bool UseCustomerAdvance = true,
    List<ParticipantInput>? Participants = null,
    [property: JsonIgnore] long? TradeCaseId = null,
    [property: JsonIgnore] bool StockAlreadyIssued = false,
    [property: JsonIgnore] bool FromOfflineSync = false,
    [property: JsonIgnore] long? OfflineActorUserId = null) : ICommand<CreateSaleResult>;

public sealed class CreateSaleCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    ILedgerService ledger,
    ICurrencyService currency,
    IStockAllocator stockAllocator,
    IBranchCatalogService branchCatalog,
    ICashbackCalculator cashbackCalculator,
    IDiscountCalculator discountCalculator,
    ISettingsService settingsService,
    IParticipantService participantService,
    IPartnerRewardService partnerRewards,
    ReceiptPrintPolicyService receiptPrinting,
    IOfflineAuthorityGuard offlineAuthority,
    IAuditService audit) : IRequestHandler<CreateSaleCommand, CreateSaleResult>
{
    public Task<CreateSaleResult> Handle(CreateSaleCommand request, CancellationToken cancellationToken) =>
        db.ExecuteInTransactionAsync(() => HandleCoreAsync(request, cancellationToken), cancellationToken);

    private async Task<CreateSaleResult> HandleCoreAsync(CreateSaleCommand request, CancellationToken cancellationToken)
    {
        var authenticatedUserId = currentUser.UserId ?? throw new UnauthorizedAccessException("Not authenticated.");
        if (request.OfflineActorUserId.HasValue && !request.FromOfflineSync)
            throw new ForbiddenException("Offline actor can only be used by the replay pipeline.");
        var userId = request.OfflineActorUserId ?? authenticatedUserId;
        if (!currentUser.HasPermission(AppPermissions.Sales.Checkout)
            && !(request.TradeCaseId.HasValue
                 && currentUser.HasPermission(AppPermissions.TradeCases.Settle)))
            throw new ForbiddenException("Sale checkout permission is required.");
        if (!request.FromQueuedCart && request.TradeCaseId is null
            && !currentUser.HasPermission(AppPermissions.Sales.Create))
            throw new ForbiddenException("Sale creation permission is required.");
        if (request.TradeCaseId is not null && !currentUser.HasPermission(AppPermissions.TradeCases.Settle))
            throw new ForbiddenException("Loyihani hisob-kitob qilishga ruxsat yo'q.");
        if (request.StockAlreadyIssued != request.TradeCaseId.HasValue)
            throw new BusinessRuleException("Saqlovdagi ombor manbasi noto'g'ri.", "invalid_custody_sale_source");

        var idempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey.Trim();
        if (idempotencyKey is not null)
        {
            var existing = await db.Sales
                .Where(s => s.UserId == userId && s.IdempotencyKey == idempotencyKey)
                .Select(s => new CreateSaleResult(s.Id, s.ReceiptToken))
                .FirstOrDefaultAsync(cancellationToken);
            if (existing is not null)
                return existing;
        }

        var warehouse = await db.Warehouses.FirstOrDefaultAsync(w => w.Id == request.WarehouseId, cancellationToken)
            ?? throw new NotFoundException("Warehouse not found.");

        if (!request.FromOfflineSync)
            await offlineAuthority.EnsureOnlineMutationAllowedAsync(warehouse.BranchId, cancellationToken);

        if (warehouse.AssignedUserId != userId &&
            await db.Warehouses.AnyAsync(w => w.AssignedUserId == userId, cancellationToken))
            throw new BusinessRuleException("Sizga biriktirilgan ombor bor — savdo faqat o'sha ombordan qilinadi.");

        if (request.TradeCaseId is { } tradeCaseId)
        {
            var validCase = await db.TradeCases.AnyAsync(x =>
                x.Id == tradeCaseId
                && x.WarehouseId == request.WarehouseId
                && x.CustomerId == request.CustomerId
                && x.Status != TradeCaseStatus.Cancelled
                && x.Status != TradeCaseStatus.Settled, cancellationToken);
            if (!validCase)
                throw new BusinessRuleException("Loyiha va savdo ma'lumotlari mos emas.", "invalid_trade_case_sale");
            if (request.Items.Any(x => x.StockId is null || x.PrepackId is not null))
                throw new BusinessRuleException("Saqlovdagi har bir qatorning ombor manbasi bo'lishi kerak.", "custody_stock_source_required");
        }

        var resolvedParticipants = await participantService.ResolveAsync(
            request.Participants, ParticipantContext.Sale, request.CustomerId, cancellationToken);

        var variantIds = request.Items.Select(i => i.VariantId).Distinct().ToList();
        var variants = await db.ProductVariants
            .Where(v => variantIds.Contains(v.Id))
            .Select(v => new
            {
                v.Id,
                v.ProductId,
                v.Product.IsEnabled,
                ProductName = v.Product.Name,
                AllowsFractional = v.Product.FractionalOverride ?? v.Product.Unit.AllowFractional
            })
            .ToListAsync(cancellationToken);

        if (variants.Count != variantIds.Count)
            throw new BusinessRuleException("Mahsulot topilmadi.");

        foreach (var item in request.Items.Where(item => item.PrepackId is null))
        {
            var variant = variants.First(v => v.Id == item.VariantId);
            if (item.Quantity != decimal.Round(item.Quantity, 3))
                throw new BusinessRuleException(
                    $"\"{variant.ProductName}\" miqdori 0.001 aniqlikdan oshmaydi.",
                    "quantity_precision_exceeded");
            if (!variant.AllowsFractional && item.Quantity != decimal.Truncate(item.Quantity))
                throw new BusinessRuleException(
                    $"\"{variant.ProductName}\" miqdori faqat butun son bo'lishi kerak.",
                    "quantity_whole_required");
        }

        if (variants.FirstOrDefault(v => !v.IsEnabled) is { } blocked)
            throw new BusinessRuleException($"\"{blocked.ProductName}\" savdo uchun yopilgan.");

        var variantProduct = variants.ToDictionary(v => v.Id, v => v.ProductId);

        var prepackIds = request.Items.Where(i => i.PrepackId is not null).Select(i => i.PrepackId!.Value).ToList();
        Dictionary<long, Prepack> prepacks = [];
        if (prepackIds.Count > 0)
        {
            if (prepackIds.Count != prepackIds.Distinct().Count())
                throw new BusinessRuleException("Bitta qadoq ikki marta qo'shilgan.");

            var now = DateTime.UtcNow;
            prepacks = await db.Prepacks.AsNoTracking()
                .Where(p => prepackIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, cancellationToken);
            foreach (var item in request.Items.Where(i => i.PrepackId is not null))
            {
                if (!prepacks.TryGetValue(item.PrepackId!.Value, out var pp) || pp.VariantId != item.VariantId || pp.WarehouseId != request.WarehouseId)
                    throw new BusinessRuleException("Qadoq topilmadi.");
            }

            var claimed = await db.Prepacks
                .Where(p => prepackIds.Contains(p.Id) && p.Status == PrepackStatus.Active && (p.ExpiresAt == null || p.ExpiresAt > now))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, PrepackStatus.Sold), cancellationToken);
            if (claimed != prepackIds.Count)
                throw new BusinessRuleException("Qadoq allaqachon sotilgan yoki muddati o'tgan.");
        }

        var shiftId = await db.Shifts
            .Where(s => s.UserId == userId && s.BranchId == warehouse.BranchId && s.Status == ShiftStatus.Open)
            .Select(s => (long?)s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var prices = await db.ProductPrices
            .Where(p => variantIds.Contains(p.VariantId) && (p.WarehouseId == warehouse.Id || p.WarehouseId == null))
            .ToListAsync(cancellationToken);

        var baseCode = (await currency.BaseAsync(cancellationToken)).ToUpperInvariant();
        var priceRates = new Dictionary<string, decimal>();
        foreach (var code in prices.Select(p => p.Currency.Trim().ToUpperInvariant()).Distinct().Where(c => c != baseCode))
            priceRates[code] = await currency.RateAsync(code, cancellationToken);

        CatalogPrice? PriceOf(long variantId)
        {
            var price = prices.FirstOrDefault(p => p.VariantId == variantId && p.WarehouseId == warehouse.Id)
                ?? prices.FirstOrDefault(p => p.VariantId == variantId && p.WarehouseId == null);
            if (price is null) return null;
            var code = price.Currency.Trim().ToUpperInvariant();
            var rate = code == baseCode ? 1m : priceRates[code];
            return new CatalogPrice(price, Math.Round(price.SellingPrice * rate, 2), code, rate);
        }

        var resolvedItems = new List<ResolvedSaleLine>();
        var priceOverrides = new List<(long VariantId, decimal CatalogPrice, decimal EnteredPrice)>();
        var priceIncreases = new Dictionary<ProductPrice, CatalogPrice>();

        foreach (var item in request.Items)
        {
            if (item.PrepackId is { } prepackId)
            {
                var prepack = prepacks[prepackId];
                resolvedItems.Add(new ResolvedSaleLine(item, prepack.Quantity, prepack.UnitPrice, baseCode, 1m, 0));
                continue;
            }

            var catalogPrice = PriceOf(item.VariantId);
            if (catalogPrice is null)
            {
                if (item.UnitPrice is null)
                    throw new BusinessRuleException($"Mahsulot narxi belgilanmagan (VariantId={item.VariantId}).");

                if (request.TradeCaseId.HasValue)
                {
                    var sourceCode = string.IsNullOrWhiteSpace(item.SourceCurrency)
                        ? baseCode
                        : item.SourceCurrency.Trim().ToUpperInvariant();
                    var fallbackRate = item.SourceRate is > 0 ? item.SourceRate.Value : 1m;
                    catalogPrice = new CatalogPrice(
                        new ProductPrice { VariantId = item.VariantId, Currency = sourceCode },
                        item.UnitPrice.Value,
                        sourceCode,
                        fallbackRate);
                }
                else
                {

                    var newPrice = await db.ProductPrices.IgnoreQueryFilters()
                        .FirstOrDefaultAsync(p => p.VariantId == item.VariantId && p.WarehouseId == null, cancellationToken);
                    if (newPrice is null)
                    {
                        newPrice = new ProductPrice { VariantId = item.VariantId, SellingPrice = 0, Currency = baseCode };
                        db.ProductPrices.Add(newPrice);
                    }
                    else
                    {
                        newPrice.IsDeleted = false;
                        newPrice.SellingPrice = 0;
                        newPrice.Currency = baseCode;
                    }
                    prices.Add(newPrice);
                    catalogPrice = new CatalogPrice(newPrice, 0, baseCode, 1m);
                }
            }

            var enteredPrice = item.UnitPrice ?? catalogPrice.Amount;
            var priceDiscount = request.TradeCaseId.HasValue
                ? 0
                : Math.Max(0, catalogPrice.Amount - enteredPrice) * item.Quantity;
            var unitPrice = request.TradeCaseId.HasValue
                ? enteredPrice
                : Math.Max(catalogPrice.Amount, enteredPrice);

            if (request.TradeCaseId is null && item.UnitPrice is not null && enteredPrice != catalogPrice.Amount)
                priceOverrides.Add((item.VariantId, catalogPrice.Amount, enteredPrice));

            if (!request.TradeCaseId.HasValue && enteredPrice > catalogPrice.Amount &&
                (!priceIncreases.TryGetValue(catalogPrice.Source, out var increase) || enteredPrice > increase.Amount))
                priceIncreases[catalogPrice.Source] = new CatalogPrice(catalogPrice.Source, enteredPrice, catalogPrice.Currency, catalogPrice.Rate);

            var sourceCurrency = request.TradeCaseId.HasValue && !string.IsNullOrWhiteSpace(item.SourceCurrency)
                ? item.SourceCurrency.Trim().ToUpperInvariant()
                : catalogPrice.Currency;
            var sourceRate = request.TradeCaseId.HasValue && item.SourceRate is > 0
                ? item.SourceRate.Value
                : catalogPrice.Rate;
            resolvedItems.Add(new ResolvedSaleLine(item, item.Quantity, unitPrice,
                sourceCurrency, sourceRate, priceDiscount));
        }

        var policy = await settingsService.GetAsync<SalesPolicySettings>(SettingKeys.SalesPolicy, cancellationToken) ?? new SalesPolicySettings();

        if (priceOverrides.Count > 0 && request.TradeCaseId is null
            && !currentUser.HasPermission(AppPermissions.Sales.PriceOverride))
            throw new ForbiddenException("Savdoda narxni o'zgartirishga ruxsat yo'q.");

        var grossAmount = resolvedItems.Sum(x => x.Quantity * x.UnitPrice);
        var priceDiscountAmount = resolvedItems.Sum(x => x.PriceDiscount);
        var discountAmount = Math.Clamp(request.DiscountAmount + priceDiscountAmount, 0, grossAmount);
        if (policy.MaxDiscountPercent > 0 && discountAmount > grossAmount * policy.MaxDiscountPercent / 100
            && !currentUser.HasPermission(AppPermissions.Sales.DiscountOverride))
            throw new BusinessRuleException($"Chegirma {policy.MaxDiscountPercent}% dan osha olmaydi.");

        if (request.ApplyAutoDiscount)
        {
            var autoApplied = await discountCalculator.CalculateAsync(request.CustomerId,
                resolvedItems.Select(x => new DiscountCalcLine(x.Item.VariantId, x.Quantity * (x.Item.UnitPrice ?? x.UnitPrice))).ToList(),
                cancellationToken);
            discountAmount = Math.Clamp(discountAmount + autoApplied.Sum(a => a.Amount), 0, grossAmount);
        }

        var totalAmount = grossAmount - discountAmount;

        var payments = new List<SalePayment>();
        decimal paidCash, paidCard, paidBonus;

        if (request.Payments is { Count: > 0 } rows)
        {
            var normalizedRows = rows
                .Where(r => r.Amount > 0)
                .Select(r => r with
                {
                    Currency = string.IsNullOrWhiteSpace(r.Currency)
                        ? baseCode
                        : r.Currency.Trim().ToUpperInvariant()
                })
                .ToList();

            if ((normalizedRows.Any(r => r.Currency != baseCode)
                 || (!string.IsNullOrWhiteSpace(request.DebtCurrency)
                     && request.DebtCurrency.Trim().ToUpperInvariant() != baseCode))
                && !await currency.IsSalesMulticurrencyAsync(cancellationToken))
                throw new BusinessRuleException("Ko'p valyuta rejimi o'chirilgan.");

            if (normalizedRows.Any(r => r.Method == PaymentMethod.Bonus && r.Currency != baseCode))
                throw new BusinessRuleException("Bonus faqat bazaviy valyutada.");

            foreach (var row in normalizedRows)
            {
                await currency.EnsureSalesAllowedAsync(row.Currency, cancellationToken);
                var rate = await currency.RateAsync(row.Currency, cancellationToken);
                payments.Add(new SalePayment
                {
                    Method = row.Method,
                    Currency = row.Currency,
                    Amount = row.Amount,
                    Rate = rate,
                    AmountBase = Math.Round(row.Amount * rate, 2)
                });
            }

            paidCash = payments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.AmountBase);
            // PaidCard is the legacy aggregate for all cashless tenders. The normalized
            // SalePayment rows retain the exact Card/Transfer/Bank distinction.
            paidCard = payments.Where(p => p.Method is PaymentMethod.Card or PaymentMethod.Transfer or PaymentMethod.Bank)
                .Sum(p => p.AmountBase);
            paidBonus = payments.Where(p => p.Method == PaymentMethod.Bonus).Sum(p => p.AmountBase);
        }
        else
        {
            paidCash = request.PaidCash;
            paidCard = request.PaidCard;
            paidBonus = request.PaidBonus;
        }

        var advanceUses = new List<AdvanceUse>();
        var remainingBeforeAdvance = Math.Max(0, totalAmount - paidCash - paidCard - paidBonus);
        if (request.UseCustomerAdvance && request.CustomerId is { } advanceCustomerId && remainingBeforeAdvance > 0)
        {
            var advanceCurrencies = await db.Accounts
                .Where(x => x.CustomerId == advanceCustomerId
                    && x.Type == AccountType.CustomerAdvance
                    && x.Balance > 0)
                .OrderByDescending(x => x.Currency == baseCode)
                .ThenBy(x => x.CreatedAt)
                .Select(x => x.Currency)
                .ToListAsync(cancellationToken);

            foreach (var advanceCurrency in advanceCurrencies)
            {
                if (remainingBeforeAdvance <= 0) break;
                var account = await ledger.FindCustomerAccountAsync(
                    advanceCustomerId, AccountType.CustomerAdvance, cancellationToken, advanceCurrency);
                if (account is not { Balance: > 0 }) continue;
                var rate = advanceCurrency == baseCode ? 1m : await currency.RateAsync(advanceCurrency, cancellationToken);
                // Never debit more native currency than the base amount being applied.
                var maxNative = Math.Floor(remainingBeforeAdvance / rate * 10_000m) / 10_000m;
                var amount = Math.Min(account.Balance, maxNative);
                var amountBase = Math.Round(amount * rate, 2);
                if (amountBase > remainingBeforeAdvance)
                {
                    amount = Math.Max(0, amount - 0.0001m);
                    amountBase = Math.Round(amount * rate, 2);
                }
                if (amount <= 0 || amountBase <= 0) continue;
                advanceUses.Add(new AdvanceUse(account, amount, rate, amountBase));
                remainingBeforeAdvance -= amountBase;
            }
        }
        var paidAdvance = advanceUses.Sum(x => x.AmountBase);
        var debtAmount = Math.Max(0, totalAmount - paidCash - paidCard - paidBonus - paidAdvance);
        var excessAmount = Math.Max(0, paidCash + paidCard + paidBonus - totalAmount);

        if (request.CreditAmount > 0)
        {
            if (!policy.AllowCustomerCredit)
                throw new BusinessRuleException("Haqdorlik funksiyasi o'chirilgan.");
            if (request.CustomerId is null)
                throw new BusinessRuleException("Haqdorlik uchun mijoz tanlanishi shart.");
            if (request.CreditAmount > excessAmount)
                throw new BusinessRuleException("Haqdorlik summasi ortiqcha to'lovdan oshib ketdi.");
            if (paidBonus > totalAmount)
                throw new BusinessRuleException("Bonus haqdorlikka o'tkazilmaydi.");
        }

        var creditAmount = request.CreditAmount;
        var changeAmount = excessAmount - creditAmount;

        if (changeAmount > 0 && paidCard + paidBonus > totalAmount + creditAmount)
            throw new BusinessRuleException("Qaytim faqat naqd to'lovdan beriladi.");

        var requiresShift = warehouse.AssignedUserId != userId && policy.ShiftPolicy switch
        {
            "AllSales" => true,
            "Off" => false,
            _ => paidCash > 0
        };
        if (requiresShift && shiftId is null)
            throw new BusinessRuleException("Naqd to'lov uchun ochiq smena talab qilinadi.");

        if (paidBonus > 0 && request.CustomerId is null)
            throw new BusinessRuleException("Bonus bilan to'lash uchun mijoz tanlanishi shart.");

        if (debtAmount > 0 && request.CustomerId is null)
            throw new BusinessRuleException("Qarzga sotish uchun mijoz tanlanishi shart.");

        if (request.CustomerId is not null && paidBonus > 0)
        {
            var bonusAccount = await ledger.FindCustomerAccountAsync(request.CustomerId.Value, AccountType.Bonus, cancellationToken);
            if ((bonusAccount?.Balance ?? 0) < paidBonus)
                throw new BusinessRuleException("Bonus balansi yetarli emas.");
        }

        var debtCurrency = debtAmount > 0 && !string.IsNullOrWhiteSpace(request.DebtCurrency)
            ? request.DebtCurrency.Trim().ToUpperInvariant()
            : baseCode;
        await currency.EnsureSalesAllowedAsync(debtCurrency, cancellationToken);
        var debtRate = debtCurrency == baseCode ? 1m : await currency.RateAsync(debtCurrency, cancellationToken);

        if (debtAmount > 0 && request.CustomerId is not null)
        {
            var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == request.CustomerId.Value, cancellationToken)
                ?? throw new NotFoundException("Customer not found.");
            if (!policy.AllowDebtSales || customer.CreditLimit > 0)
            {
                var debtAccounts = await db.Accounts
                    .Where(a => a.CustomerId == request.CustomerId.Value && a.Type == AccountType.Debt)
                    .ToListAsync(cancellationToken);
                decimal currentDebt = 0;
                foreach (var account in debtAccounts)
                    currentDebt += account.Balance * (account.Currency == baseCode ? 1m : await currency.RateAsync(account.Currency, cancellationToken));
                if (!policy.AllowDebtSales && currentDebt + debtAmount > 0)
                    throw new BusinessRuleException("Nasiya savdo o'chirilgan.");
                if (customer.CreditLimit > 0 && currentDebt + debtAmount > customer.CreditLimit)
                    throw new BusinessRuleException("Qarz limiti oshib ketdi.");
            }
        }

        var sale = new Sale
        {
            BranchId = warehouse.BranchId,
            WarehouseId = request.WarehouseId,
            UserId = userId,
            CustomerId = request.CustomerId,
            TradeCaseId = request.TradeCaseId,
            TotalAmount = totalAmount,
            DiscountAmount = discountAmount,
            PaidCash = paidCash - changeAmount,
            PaidCard = paidCard,
            PaidBonus = paidBonus,
            PaidAdvance = paidAdvance,
            DebtAmount = debtAmount,
            DebtDueDate = debtAmount > 0 ? request.DebtDueDate : null,
            DebtCurrency = debtCurrency,
            DebtRate = debtRate,
            ChangeAmount = changeAmount,
            CreditAmount = creditAmount,
            Status = SaleStatus.Completed,
            ReceiptToken = Guid.NewGuid().ToString("N"),
            IdempotencyKey = idempotencyKey,
            Payments = payments
        };
        foreach (var participant in resolvedParticipants)
            sale.Participants.Add(new SaleParticipant
            {
                RoleDefinitionId = participant.RoleDefinitionId,
                PartyId = participant.PartyId,
                PartyNameSnapshot = participant.PartyName,
                PartyPhoneSnapshot = participant.PartyPhone,
                RoleLabelSnapshot = participant.RoleLabel,
                Source = request.TradeCaseId.HasValue
                    ? ParticipantAttributionSource.CaseInherited
                    : request.FromQueuedCart
                        ? ParticipantAttributionSource.CartInherited
                        : ParticipantAttributionSource.Direct
            });

        var cashbackFactor = grossAmount > 0 ? totalAmount / grossAmount : 1m;
        Dictionary<long, Stock> issuedStocks = [];
        if (request.StockAlreadyIssued)
        {
            var stockIds = resolvedItems.Select(x => x.Item.StockId!.Value).Distinct().ToList();
            issuedStocks = await db.Stocks
                .Where(x => stockIds.Contains(x.Id) && x.WarehouseId == request.WarehouseId)
                .ToDictionaryAsync(x => x.Id, cancellationToken);
            if (issuedStocks.Count != stockIds.Count
                || resolvedItems.Any(x => issuedStocks[x.Item.StockId!.Value].VariantId != x.Item.VariantId))
                throw new BusinessRuleException("Saqlovdagi mahsulot partiyasi mos emas.", "invalid_custody_stock_source");
        }
        else
        {
            await stockAllocator.PreloadAsync(request.WarehouseId,
                resolvedItems.Select(x => x.Item.VariantId), cancellationToken);
        }

        foreach (var line in resolvedItems)
        {
            if (request.StockAlreadyIssued)
            {
                var stock = issuedStocks[line.Item.StockId!.Value];
                sale.Items.Add(new SaleItem
                {
                    VariantId = line.Item.VariantId,
                    StockId = stock.Id,
                    Stock = stock,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    PriceCurrency = line.Currency,
                    PriceRate = line.Rate,
                    PurchasePrice = stock.PurchasePrice
                });
                continue;
            }

            var allocations = await stockAllocator.AllocateAsync(request.WarehouseId, line.Item.VariantId, line.Quantity, policy.AllowInsufficientStockSales, cancellationToken);

            foreach (var allocation in allocations)
            {
                sale.Items.Add(new SaleItem
                {
                    VariantId = line.Item.VariantId,
                    StockId = allocation.Batch.Id,
                    Stock = allocation.Batch,
                    Quantity = allocation.Quantity,
                    UnitPrice = line.UnitPrice,
                    PriceCurrency = line.Currency,
                    PriceRate = line.Rate,
                    PurchasePrice = allocation.Batch.PurchasePrice
                });

                allocation.Batch.Quantity -= allocation.Quantity;
            }
        }

        foreach (var increase in priceIncreases.Values)
            increase.Source.SellingPrice = Math.Round(increase.Amount / increase.Rate, 2);

        await branchCatalog.ActivateAsync(warehouse.BranchId, variantIds, BranchCatalogActivationSource.Sale, cancellationToken);
        db.Sales.Add(sale);

        await PostLedgerAsync(sale, warehouse.BranchId, debtAmount, advanceUses,
            variantProduct, cashbackFactor, userId, shiftId, cancellationToken);
        await partnerRewards.AccrueSaleAsync(sale, cancellationToken);

        sale.RaiseDomainEvent(new SaleCompletedEvent(sale.ReceiptToken, sale.CustomerId, sale.TotalAmount));
        sale.RaiseDomainEvent(new ReceiptMirrorEvent(sale.ReceiptToken));

        // The job is a durable outbox row written atomically with the sale. A
        // background router assigns it after commit, so checkout never waits on
        // an OS printer or a network print host.
        if (!request.FromOfflineSync)
            await receiptPrinting.EnqueueAutomaticReceiptAsync(sale, cancellationToken);

        if (priceOverrides.Count > 0)
            audit.Add("priceOverride", "sales", null,
                priceOverrides.Select(x => new { x.VariantId, x.CatalogPrice, x.EnteredPrice }));

        if (priceIncreases.Count > 0)
            audit.Add("salePriceUp", "product_prices", null,
                priceIncreases.Values.Select(x => new { x.Source.VariantId, x.Source.WarehouseId, SellingPrice = x.Amount }));

        await db.SaveChangesAsync(cancellationToken);

        audit.SetOutcome("sale.completed", "sales", sale.Id, new
        {
            sale.ReceiptToken,
            sale.BranchId,
            sale.WarehouseId,
            sale.CustomerId,
            sale.TradeCaseId,
            sale.TotalAmount,
            sale.DiscountAmount,
            sale.PaidCash,
            sale.PaidCard,
            sale.PaidBonus,
            sale.PaidAdvance,
            sale.DebtAmount,
            sale.DebtCurrency,
            sale.ChangeAmount,
            sale.CreditAmount,
            origin = request.FromOfflineSync ? "offlineSync" : request.FromQueuedCart ? "queue" : "online",
            payments = sale.Payments.Select(x => new { x.Method, x.Currency, x.Amount, x.Rate, x.AmountBase }),
            participants = sale.Participants.Select(x => new { x.RoleDefinitionId, x.PartyId, x.RoleLabelSnapshot }),
            items = resolvedItems.Select(x => new { x.Item.VariantId, x.Quantity, x.UnitPrice, x.Currency, x.Rate })
        }, "Savdo amalga oshirildi", sale.BranchId, request.OfflineActorUserId);

        if (prepackIds.Count > 0)
            await db.Prepacks.Where(p => prepackIds.Contains(p.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.SoldSaleId, sale.Id), cancellationToken);

        return new CreateSaleResult(sale.Id, sale.ReceiptToken);
    }

    private async Task PostLedgerAsync(
        Sale sale,
        long branchId,
        decimal debtAmount,
        IReadOnlyCollection<AdvanceUse> advanceUses,
        IReadOnlyDictionary<long, long> variantProduct,
        decimal cashbackFactor,
        long userId,
        long? shiftId,
        CancellationToken cancellationToken)
    {
        void Post(OperationType type, decimal amount, Account? from, Account? to, decimal rate = 1m)
        {
            var transaction = ledger.Post(type, amount, from, to, userId, shiftId, rate);
            transaction.Sale = sale;
            transaction.TradeCaseId = sale.TradeCaseId;
        }

        if (sale.Payments.Count > 0)
        {
            foreach (var payment in sale.Payments.Where(p => p.Method != PaymentMethod.Bonus))
            {
                var type = payment.Method switch
                {
                    PaymentMethod.Cash => AccountType.Cash,
                    PaymentMethod.Card => AccountType.Card,
                    PaymentMethod.Transfer => AccountType.Transfer,
                    PaymentMethod.Bank => AccountType.Bank,
                    _ => throw new BusinessRuleException("Qo'llab-quvvatlanmaydigan to'lov turi.", "unsupported_payment_method")
                };
                var account = await ledger.BranchAccountAsync(branchId, type, cancellationToken, payment.Currency);
                Post(OperationType.Sale, payment.Amount, null, account, payment.Rate);
            }

            if (sale.ChangeAmount > 0)
            {
                var cash = await ledger.BranchAccountAsync(branchId, AccountType.Cash, cancellationToken);
                Post(OperationType.Change, sale.ChangeAmount, cash, null);
            }
        }
        else
        {
            if (sale.PaidCash > 0)
            {
                var cash = await ledger.BranchAccountAsync(branchId, AccountType.Cash, cancellationToken);
                Post(OperationType.Sale, sale.PaidCash, null, cash);
            }

            if (sale.PaidCard > 0)
            {
                var card = await ledger.BranchAccountAsync(branchId, AccountType.Card, cancellationToken);
                Post(OperationType.Sale, sale.PaidCard, null, card);
            }
        }

        if (sale.CustomerId is null)
            return;

        var customerId = sale.CustomerId.Value;

        if (sale.PaidBonus > 0)
        {
            var bonus = await ledger.CustomerAccountAsync(customerId, AccountType.Bonus, cancellationToken);
            Post(OperationType.BonusSpend, sale.PaidBonus, bonus, null);
        }

        foreach (var advanceUse in advanceUses)
            Post(OperationType.CustomerAdvance, advanceUse.Amount, advanceUse.Account, null, advanceUse.Rate);

        if (debtAmount > 0)
        {
            var debt = await ledger.CustomerAccountAsync(customerId, AccountType.Debt, cancellationToken, sale.DebtCurrency);
            var debtInCurrency = sale.DebtRate == 1m ? debtAmount : Math.Round(debtAmount / sale.DebtRate, 2);
            Post(OperationType.DebtCharge, debtInCurrency, null, debt, sale.DebtRate);
        }

        if (sale.CreditAmount > 0)
        {
            var advance = await ledger.CustomerAccountAsync(customerId, AccountType.CustomerAdvance, cancellationToken);
            Post(OperationType.CustomerAdvance, sale.CreditAmount, null, advance);
        }

        var saleItems = sale.Items.ToList();
        var cashback = await cashbackCalculator.CalculateBreakdownAsync(branchId,
            saleItems.Select(x => new CashbackLine(
                variantProduct[x.VariantId], x.Quantity, x.UnitPrice * x.Quantity * cashbackFactor)).ToList(),
            cancellationToken);
        if (cashback.Total > 0)
        {
            for (var i = 0; i < saleItems.Count; i++)
                saleItems[i].CashbackEarned = cashback.LineAmounts[i];
            var remainingCashback = cashback.Total;
            var recovery = await ledger.FindCustomerAccountAsync(customerId, AccountType.RewardRecovery, cancellationToken);
            if (recovery is { Balance: > 0 })
            {
                var recovered = Math.Min(recovery.Balance, remainingCashback);
                Post(OperationType.CashbackRecovery, recovered, recovery, null);
                remainingCashback -= recovered;
            }
            if (remainingCashback > 0)
            {
                var bonus = await ledger.CustomerAccountAsync(customerId, AccountType.Bonus, cancellationToken);
                Post(OperationType.Cashback, remainingCashback, null, bonus);
            }
            sale.CashbackEarned = cashback.Total;
        }
    }
}

public sealed class CreateSaleCommandValidator : AbstractValidator<CreateSaleCommand>
{
    public CreateSaleCommandValidator()
    {
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).Must(i => i.Quantity > 0).WithMessage("Miqdor 0 dan katta bo'lishi kerak.");
        RuleForEach(x => x.Items).Must(i => i.UnitPrice is null || i.UnitPrice >= 0).WithMessage("Narx manfiy bo'lishi mumkin emas.");
        RuleFor(x => x.PaidCash).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PaidCard).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PaidBonus).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CreditAmount).GreaterThanOrEqualTo(0);
    }
}
