using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Domain.Events;
using Cartex.Domain.Pricing;
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

/// One stock batch taken for one requested line. Discounts are allocated onto these rows,
/// so the sale total and the stored lines are derived from the very same numbers.
file sealed class SaleRow(int lineIndex, Stock batch, decimal quantity, ResolvedSaleLine line)
{
    public int LineIndex { get; } = lineIndex;
    public Stock Batch { get; } = batch;
    public decimal Quantity { get; } = quantity;
    public ResolvedSaleLine Line { get; } = line;
    public decimal Extended { get; } = Math.Round(quantity * line.UnitPrice, 2);
    public decimal Discount { get; set; }

    /// What the row is actually worth once its own price cut is off. Order-level discounts are
    /// shared out by this, not by the catalog amount: a percent quoted on the payable has to
    /// come off each line in proportion to what the customer is really paying for it.
    public decimal NetBase { get; set; }
}

internal sealed record AdvanceUse(Account Account, decimal Amount, decimal Rate, decimal AmountBase);

public sealed record CreateSaleCommand(
    long WarehouseId,
    long? CustomerId,
    decimal PaidCash,
    decimal PaidCard,
    decimal PaidBonus,
    List<CreateSaleItemDto> Items) : ICommand<CreateSaleResult>
{
    public decimal DiscountAmount { get; init; }
    public List<SalePaymentDto>? Payments { get; init; }
    public string? DebtCurrency { get; init; }
    public DateOnly? DebtDueDate { get; init; }
    public string? IdempotencyKey { get; init; }
    public bool ApplyAutoDiscount { get; init; } = true;
    public decimal CreditAmount { get; init; }
    public bool FromQueuedCart { get; init; }
    public bool UseCustomerAdvance { get; init; } = true;
    public List<ParticipantInput>? Participants { get; init; }
    public string? Note { get; init; }
    public decimal RoundingAmount { get; init; }

    [JsonIgnore] public bool FromOfflineSync { get; init; }
    [JsonIgnore] public long? OfflineActorUserId { get; init; }
    [JsonIgnore] public IReadOnlyDictionary<long, decimal>? PreauthorizedPrices { get; init; }
    [JsonIgnore] public decimal? PreauthorizedDiscountAmount { get; init; }
    [JsonIgnore] public decimal? PreauthorizedRoundingAmount { get; init; }
}

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
        if (!currentUser.HasPermission(AppPermissions.Sales.Checkout))
            throw new ForbiddenException("Sale checkout permission is required.");
        if (!request.FromQueuedCart && !currentUser.HasPermission(AppPermissions.Sales.Create))
            throw new ForbiddenException("Sale creation permission is required.");

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

            var enteredPrice = item.UnitPrice ?? catalogPrice.Amount;
            var priceDiscount = Math.Max(0, catalogPrice.Amount - enteredPrice) * item.Quantity;
            var unitPrice = Math.Max(catalogPrice.Amount, enteredPrice);

            if (item.UnitPrice is not null && enteredPrice != catalogPrice.Amount)
                priceOverrides.Add((item.VariantId, catalogPrice.Amount, enteredPrice));

            if (enteredPrice > catalogPrice.Amount &&
                (!priceIncreases.TryGetValue(catalogPrice.Source, out var increase) || enteredPrice > increase.Amount))
                priceIncreases[catalogPrice.Source] = new CatalogPrice(catalogPrice.Source, enteredPrice, catalogPrice.Currency, catalogPrice.Rate);

            resolvedItems.Add(new ResolvedSaleLine(item, item.Quantity, unitPrice,
                catalogPrice.Currency, catalogPrice.Rate, priceDiscount));
        }

        var policy = await settingsService.GetAsync<SalesPolicySettings>(SettingKeys.SalesPolicy, cancellationToken) ?? new SalesPolicySettings();

        // Navbatdagi savatga ruxsatli foydalanuvchi kiritib qo'ygan narx yakunlovchidan
        // qayta ruxsat talab qilmaydi; faqat yangi/o'zgartirilgan narx tekshiriladi.
        if (priceOverrides.Any(o => request.PreauthorizedPrices is null
                || !request.PreauthorizedPrices.TryGetValue(o.VariantId, out var preauthorized)
                || preauthorized != o.EnteredPrice)
            && !currentUser.HasPermission(AppPermissions.Sales.PriceOverride))
            throw new ForbiddenException("Savdoda narxni o'zgartirishga ruxsat yo'q.");

        // Navbatdagi savatga ruxsatli sotuvchi kiritgan chegirma yakunlovchidan qayta ruxsat
        // talab qilmaydi — narx o'zgartirish bilan bir xil mantiq. Faqat o'zgartirilgani tekshiriladi.
        var preauthorizedDiscount = request.PreauthorizedDiscountAmount ?? 0m;
        var preauthorizedRounding = request.PreauthorizedRoundingAmount ?? 0m;
        if ((request.DiscountAmount != preauthorizedDiscount || request.RoundingAmount != preauthorizedRounding)
            && (request.DiscountAmount > 0 || request.RoundingAmount > 0)
            && !currentUser.HasPermission(AppPermissions.Sales.Discount))
            throw new ForbiddenException("Savdoda chegirma berishga ruxsat yo'q.");

        // Stock is allocated before the totals so the sale adds up from the very rows it will
        // store: a line split across batches rounds once per row, not once per line.
        await stockAllocator.PreloadAsync(request.WarehouseId,
            resolvedItems.Select(x => x.Item.VariantId), cancellationToken);

        var saleRows = new List<SaleRow>();
        for (var index = 0; index < resolvedItems.Count; index++)
        {
            var line = resolvedItems[index];
            var allocations = await stockAllocator.AllocateAsync(request.WarehouseId, line.Item.VariantId,
                line.Quantity, policy.AllowInsufficientStockSales, cancellationToken);
            foreach (var allocation in allocations)
            {
                saleRows.Add(new SaleRow(index, allocation.Batch, allocation.Quantity, line));
                allocation.Batch.Quantity -= allocation.Quantity;
            }
        }

        // Each component is capped by what its rows have left, so no line can go below zero;
        // anything that will not fit is dropped from the header too, keeping the two in step.
        decimal Place(decimal amount, IReadOnlyList<SaleRow> targets, Func<SaleRow, decimal> weight)
        {
            if (amount <= 0 || targets.Count == 0) return amount;
            var placement = MoneyAllocator.Distribute(amount,
                [.. targets.Select(weight)],
                [.. targets.Select(r => r.Extended - r.Discount)]);
            for (var i = 0; i < targets.Count; i++) targets[i].Discount += placement.Placed[i];
            return placement.Residual;
        }

        var rowsByLine = Enumerable.Range(0, resolvedItems.Count)
            .Select(index => saleRows.Where(r => r.LineIndex == index).ToList())
            .ToList();

        var grossAmount = saleRows.Sum(r => r.Extended);

        var priceDiscountAmount = 0m;
        for (var index = 0; index < resolvedItems.Count; index++)
        {
            var cut = Math.Round(resolvedItems[index].PriceDiscount, 2);
            if (cut <= 0) continue;
            priceDiscountAmount += cut - Place(cut, rowsByLine[index], r => r.Quantity);
        }

        foreach (var row in saleRows) row.NetBase = row.Extended - row.Discount;

        var discountAmount = Math.Clamp(request.DiscountAmount + priceDiscountAmount, 0, grossAmount);
        if (policy.MaxDiscountPercent > 0 && discountAmount > grossAmount * policy.MaxDiscountPercent / 100
            && !currentUser.HasPermission(AppPermissions.Sales.DiscountOverride))
            throw new BusinessRuleException($"Chegirma {policy.MaxDiscountPercent}% dan osha olmaydi.");

        discountAmount -= Place(discountAmount - priceDiscountAmount, saleRows, r => r.NetBase);

        if (request.ApplyAutoDiscount)
        {
            var autoApplied = await discountCalculator.CalculateAsync(request.CustomerId,
                resolvedItems.Select(x => new DiscountCalcLine(x.Item.VariantId, x.Quantity * (x.Item.UnitPrice ?? x.UnitPrice))).ToList(),
                cancellationToken);

            // A rule keeps to the lines it matched. Spreading it over the whole basket would make
            // the products it never touched look discounted, and tomorrow they would be refunded short.
            var budget = grossAmount - discountAmount;
            foreach (var application in autoApplied)
            {
                if (application.LineAmounts is not { } shares)
                {
                    var flat = Math.Clamp(application.Amount, 0, budget);
                    var spread = flat - Place(flat, saleRows, r => r.NetBase);
                    discountAmount += spread;
                    budget -= spread;
                    continue;
                }

                for (var index = 0; index < shares.Count && budget > 0; index++)
                {
                    var share = Math.Min(shares[index], budget);
                    if (share <= 0) continue;
                    var placed = share - Place(share, rowsByLine[index], r => r.NetBase);
                    discountAmount += placed;
                    budget -= placed;
                }
            }
        }

        // Yaxlitlash — chegirmaning bir turi: u ham qatorlarga tushadi, shuning uchun ertaga
        // qaytarilganda o'z qatoridan chiqadi. Sarlavhada esa alohida izoh sifatida saqlanadi.
        var roundingAmount = 0m;
        if (request.RoundingAmount > 0)
        {
            if (!policy.AllowRounding)
                throw new BusinessRuleException("Yaxlitlash o'chirilgan.", "rounding_disabled");
            if (policy.MaxRoundingAmount > 0 && request.RoundingAmount > policy.MaxRoundingAmount
                && !currentUser.HasPermission(AppPermissions.Sales.DiscountOverride))
                throw new ForbiddenException($"Yaxlitlash {policy.MaxRoundingAmount:N0} dan osha olmaydi.");

            if (request.RoundingAmount > grossAmount - discountAmount)
                throw new BusinessRuleException("Yaxlitlash to'lanadigan summadan oshib ketdi.", "rounding_exceeds_payable");

            roundingAmount = request.RoundingAmount - Place(request.RoundingAmount, saleRows, r => r.NetBase);
            discountAmount += roundingAmount;
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
            TotalAmount = totalAmount,
            DiscountAmount = discountAmount,
            RoundingAmount = roundingAmount,
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
            ShiftId = shiftId,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
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
                Source = request.FromQueuedCart
                    ? ParticipantAttributionSource.CartInherited
                    : ParticipantAttributionSource.Direct
            });

        foreach (var row in saleRows)
            sale.Items.Add(new SaleItem
            {
                VariantId = row.Line.Item.VariantId,
                StockId = row.Batch.Id,
                Stock = row.Batch,
                Quantity = row.Quantity,
                UnitPrice = row.Line.UnitPrice,
                DiscountAmount = row.Discount,
                PriceCurrency = row.Line.Currency,
                PriceRate = row.Line.Rate,
                PurchasePrice = row.Batch.PurchasePrice
            });

        // NARX-06/07: the sale is priced already; this only decides whether the catalogue follows.
        // One mistyped price must not be able to rewrite the catalogue, but it must not stop the
        // sale either — the customer is standing at the till.
        var skippedIncreases = new List<CatalogPrice>();
        if (!policy.UpdateCatalogPriceOnSale)
        {
            skippedIncreases.AddRange(priceIncreases.Values);
            priceIncreases.Clear();
        }
        else if (policy.MaxPriceIncreasePercent > 0)
            foreach (var increase in priceIncreases.Values.ToList())
            {
                // A product priced at zero is being given its first price, not raised (NARX-08).
                var current = Math.Round(increase.Source.SellingPrice * increase.Rate, 2);
                if (current <= 0 || (increase.Amount - current) / current * 100 <= policy.MaxPriceIncreasePercent)
                    continue;
                skippedIncreases.Add(increase);
                priceIncreases.Remove(increase.Source);
            }

        foreach (var increase in priceIncreases.Values)
            increase.Source.SellingPrice = Math.Round(increase.Amount / increase.Rate, 2);

        await branchCatalog.ActivateAsync(warehouse.BranchId, variantIds, BranchCatalogActivationSource.Sale, cancellationToken);
        db.Sales.Add(sale);

        await PostLedgerAsync(sale, warehouse.BranchId, debtAmount, advanceUses,
            variantProduct, userId, shiftId, cancellationToken);
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

        if (skippedIncreases.Count > 0)
            audit.Add("salePriceUpSkipped", "product_prices", null,
                skippedIncreases.Select(x => new { x.Source.VariantId, x.Source.WarehouseId, Entered = x.Amount }));

        await db.SaveChangesAsync(cancellationToken);

        audit.SetOutcome("sale.completed", "sales", sale.Id, new
        {
            sale.ReceiptToken,
            sale.BranchId,
            sale.WarehouseId,
            sale.CustomerId,
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
        long userId,
        long? shiftId,
        CancellationToken cancellationToken)
    {
        void Post(OperationType type, decimal amount, Account? from, Account? to, decimal rate = 1m)
        {
            var transaction = ledger.Post(type, amount, from, to, userId, shiftId, rate);
            transaction.Sale = sale;
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
                variantProduct[x.VariantId], x.Quantity, x.Quantity * x.UnitPrice - x.DiscountAmount)).ToList(),
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
        RuleFor(x => x.DiscountAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.RoundingAmount).GreaterThanOrEqualTo(0);
    }
}
