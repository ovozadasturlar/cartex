using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
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
using Cartex.Application.Common.Inventory;
using Cartex.Application.Common.Loyalty;

namespace Cartex.Application.Sales.Commands;

public record CreateSaleItemDto(long VariantId, decimal Quantity, decimal? UnitPrice = null, long? PrepackId = null);

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
    bool FromQueuedCart = false) : ICommand<CreateSaleResult>;

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
    IAuditService audit) : IRequestHandler<CreateSaleCommand, CreateSaleResult>
{
    public Task<CreateSaleResult> Handle(CreateSaleCommand request, CancellationToken cancellationToken) =>
        db.ExecuteInTransactionAsync(() => HandleCoreAsync(request, cancellationToken), cancellationToken);

    private async Task<CreateSaleResult> HandleCoreAsync(CreateSaleCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("Not authenticated.");
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

        if (warehouse.AssignedUserId != userId &&
            await db.Warehouses.AnyAsync(w => w.AssignedUserId == userId, cancellationToken))
            throw new BusinessRuleException("Sizga biriktirilgan ombor bor — savdo faqat o'sha ombordan qilinadi.");

        var variantIds = request.Items.Select(i => i.VariantId).Distinct().ToList();
        var variants = await db.ProductVariants
            .Where(v => variantIds.Contains(v.Id))
            .Select(v => new { v.Id, v.ProductId, v.Product.IsEnabled, ProductName = v.Product.Name })
            .ToListAsync(cancellationToken);

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

        var baseCode = await currency.BaseAsync(cancellationToken);
        var priceRates = new Dictionary<string, decimal>();
        foreach (var code in prices.Select(p => p.Currency).Distinct().Where(c => c != baseCode))
            priceRates[code] = await currency.RateAsync(code, cancellationToken);

        CatalogPrice? PriceOf(long variantId)
        {
            var price = prices.FirstOrDefault(p => p.VariantId == variantId && p.WarehouseId == warehouse.Id)
                ?? prices.FirstOrDefault(p => p.VariantId == variantId && p.WarehouseId == null);
            if (price is null) return null;
            var rate = price.Currency == baseCode ? 1m : priceRates[price.Currency];
            return new CatalogPrice(price, Math.Round(price.SellingPrice * rate, 2), price.Currency, rate);
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

            resolvedItems.Add(new ResolvedSaleLine(item, item.Quantity, unitPrice, catalogPrice.Currency, catalogPrice.Rate, priceDiscount));
        }

        var policy = await settingsService.GetAsync<SalesPolicySettings>(SettingKeys.SalesPolicy, cancellationToken) ?? new SalesPolicySettings();

        if (priceOverrides.Count > 0 && !currentUser.HasPermission(AppPermissions.Sales.PriceOverride))
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
            if ((rows.Any(r => r.Currency != baseCode) || (request.DebtCurrency is not null && request.DebtCurrency != baseCode))
                && !await currency.IsMulticurrencyAsync(cancellationToken))
                throw new BusinessRuleException("Ko'p valyuta rejimi o'chirilgan.");

            if (rows.Any(r => r.Method == PaymentMethod.Bonus && r.Currency != baseCode))
                throw new BusinessRuleException("Bonus faqat bazaviy valyutada.");

            foreach (var row in rows.Where(r => r.Amount > 0))
            {
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
            paidCard = payments.Where(p => p.Method == PaymentMethod.Card).Sum(p => p.AmountBase);
            paidBonus = payments.Where(p => p.Method == PaymentMethod.Bonus).Sum(p => p.AmountBase);
        }
        else
        {
            paidCash = request.PaidCash;
            paidCard = request.PaidCard;
            paidBonus = request.PaidBonus;
        }

        var debtAmount = Math.Max(0, totalAmount - paidCash - paidCard - paidBonus);
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

        if ((paidBonus > 0 || debtAmount > 0) && request.CustomerId is null)
            throw new BusinessRuleException("Bonus to'lov yoki qarz uchun mijoz tanlanishi shart.");

        if (request.CustomerId is not null && paidBonus > 0)
        {
            var bonusAccount = await ledger.FindCustomerAccountAsync(request.CustomerId.Value, AccountType.Bonus, cancellationToken);
            if ((bonusAccount?.Balance ?? 0) < paidBonus)
                throw new BusinessRuleException("Bonus balansi yetarli emas.");
        }

        var debtCurrency = debtAmount > 0 ? request.DebtCurrency ?? baseCode : baseCode;
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
            PaidCash = paidCash - changeAmount,
            PaidCard = paidCard,
            PaidBonus = paidBonus,
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

        var cashbackFactor = grossAmount > 0 ? totalAmount / grossAmount : 1m;
        var cashbackLines = new List<CashbackLine>();

        await stockAllocator.PreloadAsync(request.WarehouseId, resolvedItems.Select(x => x.Item.VariantId), cancellationToken);

        foreach (var line in resolvedItems)
        {
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

            cashbackLines.Add(new CashbackLine(variantProduct[line.Item.VariantId], line.Quantity, line.UnitPrice * line.Quantity * cashbackFactor));
        }

        foreach (var increase in priceIncreases.Values)
            increase.Source.SellingPrice = Math.Round(increase.Amount / increase.Rate, 2);

        await branchCatalog.ActivateAsync(warehouse.BranchId, variantIds, BranchCatalogActivationSource.Sale, cancellationToken);
        db.Sales.Add(sale);

        await PostLedgerAsync(sale, warehouse.BranchId, debtAmount, cashbackLines, userId, shiftId, cancellationToken);

        sale.RaiseDomainEvent(new SaleCompletedEvent(sale.ReceiptToken, sale.BranchId, sale.CustomerId, sale.TotalAmount));
        sale.RaiseDomainEvent(new ReceiptMirrorEvent(sale.ReceiptToken));

        if (priceOverrides.Count > 0)
            audit.Add("priceOverride", "sales", null,
                priceOverrides.Select(x => new { x.VariantId, x.CatalogPrice, x.EnteredPrice }));

        if (priceIncreases.Count > 0)
            audit.Add("salePriceUp", "product_prices", null,
                priceIncreases.Values.Select(x => new { x.Source.VariantId, x.Source.WarehouseId, SellingPrice = x.Amount }));

        await db.SaveChangesAsync(cancellationToken);

        if (prepackIds.Count > 0)
            await db.Prepacks.Where(p => prepackIds.Contains(p.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.SoldSaleId, sale.Id), cancellationToken);

        return new CreateSaleResult(sale.Id, sale.ReceiptToken);
    }

    private async Task PostLedgerAsync(Sale sale, long branchId, decimal debtAmount, List<CashbackLine> cashbackLines, long userId, long? shiftId, CancellationToken cancellationToken)
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
                var type = payment.Method == PaymentMethod.Cash ? AccountType.Cash : AccountType.Card;
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

        if (debtAmount > 0)
        {
            var debt = await ledger.CustomerAccountAsync(customerId, AccountType.Debt, cancellationToken, sale.DebtCurrency);
            var debtInCurrency = sale.DebtRate == 1m ? debtAmount : Math.Round(debtAmount / sale.DebtRate, 2);
            Post(OperationType.DebtCharge, debtInCurrency, null, debt, sale.DebtRate);
        }

        if (sale.CreditAmount > 0)
        {
            var debt = await ledger.CustomerAccountAsync(customerId, AccountType.Debt, cancellationToken);
            Post(OperationType.CustomerCredit, sale.CreditAmount, debt, null);
        }

        var cashback = await cashbackCalculator.CalculateAsync(branchId, cashbackLines, cancellationToken);
        if (cashback > 0)
        {
            var bonus = await ledger.CustomerAccountAsync(customerId, AccountType.Bonus, cancellationToken);
            Post(OperationType.Cashback, cashback, null, bonus);
            sale.CashbackEarned = cashback;
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
