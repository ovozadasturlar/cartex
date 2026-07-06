using Cartex.Application.Common.Messaging;
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
using Cartex.Domain.Measurement;

namespace Cartex.Application.Sales.Commands;

public record CreateSaleItemDto(long VariantId, decimal Quantity, decimal? UnitPrice = null, long? UnitId = null);

public record SalePaymentDto(PaymentMethod Method, string Currency, decimal Amount);

public record CreateSaleResult(long SaleId, string ReceiptToken);

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
    DateOnly? DebtDueDate = null) : ICommand<CreateSaleResult>;

public sealed class CreateSaleCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    ILedgerService ledger,
    ICurrencyService currency,
    IStockAllocator stockAllocator,
    ICashbackCalculator cashbackCalculator,
    IAuditService audit) : IRequestHandler<CreateSaleCommand, CreateSaleResult>
{
    public async Task<CreateSaleResult> Handle(CreateSaleCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("Not authenticated.");

        var hasOverride = request.Items.Any(i => i.UnitPrice is not null);
        if (hasOverride && !currentUser.HasPermission(AppPermissions.Sales.PriceOverride))
            throw new ForbiddenException("Savdoda narxni o'zgartirishga ruxsat yo'q.");

        var warehouse = await db.Warehouses.FirstOrDefaultAsync(w => w.Id == request.WarehouseId, cancellationToken)
            ?? throw new NotFoundException("Warehouse not found.");

        var shiftId = await db.Shifts
            .Where(s => s.UserId == userId && s.BranchId == warehouse.BranchId && s.Status == ShiftStatus.Open)
            .Select(s => (long?)s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var variantIds = request.Items.Select(i => i.VariantId).Distinct().ToList();
        var prices = await db.ProductPrices
            .Where(p => variantIds.Contains(p.VariantId) && (p.WarehouseId == warehouse.Id || p.WarehouseId == null))
            .ToListAsync(cancellationToken);

        var variantProduct = await db.ProductVariants
            .Where(v => variantIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, v => v.ProductId, cancellationToken);

        var baseCode = await currency.BaseAsync(cancellationToken);
        var priceRates = new Dictionary<string, decimal>();
        foreach (var code in prices.Select(p => p.Currency).Distinct().Where(c => c != baseCode))
            priceRates[code] = await currency.RateAsync(code, cancellationToken);

        (decimal Price, string Currency, decimal Rate) PriceOf(long variantId)
        {
            var price = prices.FirstOrDefault(p => p.VariantId == variantId && p.WarehouseId == warehouse.Id)
                ?? prices.FirstOrDefault(p => p.VariantId == variantId && p.WarehouseId == null)
                ?? throw new BusinessRuleException($"Mahsulot narxi belgilanmagan (VariantId={variantId}).");
            var rate = price.Currency == baseCode ? 1m : priceRates[price.Currency];
            return (Math.Round(price.SellingPrice * rate, 2), price.Currency, rate);
        }

        var lineUnitIds = request.Items.Where(i => i.UnitId is not null).Select(i => i.UnitId!.Value).Distinct().ToList();
        Dictionary<long, Cartex.Domain.Entities.Unit> lineUnits = [];
        Dictionary<long, Cartex.Domain.Entities.Unit> stockingUnits = [];
        if (lineUnitIds.Count > 0)
        {
            lineUnits = await db.Units.Where(u => lineUnitIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, cancellationToken);
            stockingUnits = await db.ProductVariants
                .Where(v => variantIds.Contains(v.Id))
                .Select(v => new { v.Id, v.Product.Unit })
                .ToDictionaryAsync(x => x.Id, x => x.Unit, cancellationToken);
        }

        (decimal Quantity, decimal Price, string Currency, decimal Rate) Resolve(CreateSaleItemDto item)
        {
            var (price, priceCurrency, priceRate) = item.UnitPrice is { } overridePrice
                ? (overridePrice, baseCode, 1m)
                : PriceOf(item.VariantId);
            if (item.UnitId is { } uid && lineUnits.TryGetValue(uid, out var from))
            {
                var stocking = stockingUnits[item.VariantId];
                if (item.UnitPrice is not null)
                    price = UnitConversion.PricePerBase(item.UnitPrice.Value, from, stocking);
                return (UnitConversion.ToBase(item.Quantity, from, stocking), price, priceCurrency, priceRate);
            }
            return (item.Quantity, price, priceCurrency, priceRate);
        }

        var resolvedItems = request.Items.Select(i => (item: i, line: Resolve(i))).ToList();
        var grossAmount = resolvedItems.Sum(x => x.line.Quantity * x.line.Price);
        var discountAmount = Math.Clamp(request.DiscountAmount, 0, grossAmount);
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
        var changeAmount = Math.Max(0, paidCash + paidCard + paidBonus - totalAmount);

        if (changeAmount > 0 && paidCard + paidBonus > totalAmount)
            throw new BusinessRuleException("Qaytim faqat naqd to'lovdan beriladi.");

        if (paidCash > 0 && shiftId is null)
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
            if (customer.CreditLimit > 0)
            {
                var debtAccounts = await db.Accounts
                    .Where(a => a.CustomerId == request.CustomerId.Value && a.Type == AccountType.Debt)
                    .ToListAsync(cancellationToken);
                decimal currentDebt = 0;
                foreach (var account in debtAccounts)
                    currentDebt += account.Balance * (account.Currency == baseCode ? 1m : await currency.RateAsync(account.Currency, cancellationToken));
                if (currentDebt + debtAmount > customer.CreditLimit)
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
            Status = SaleStatus.Completed,
            ReceiptToken = Guid.NewGuid().ToString("N"),
            Payments = payments
        };

        var cashbackFactor = grossAmount > 0 ? totalAmount / grossAmount : 1m;
        var cashbackLines = new List<CashbackLine>();

        foreach (var (item, line) in resolvedItems)
        {
            var allocations = await stockAllocator.AllocateAsync(request.WarehouseId, item.VariantId, line.Quantity, cancellationToken);

            foreach (var allocation in allocations)
            {
                sale.Items.Add(new SaleItem
                {
                    VariantId = item.VariantId,
                    StockId = allocation.Batch.Id,
                    Quantity = allocation.Quantity,
                    UnitPrice = line.Price,
                    PriceCurrency = line.Currency,
                    PriceRate = line.Rate,
                    PurchasePrice = allocation.Batch.PurchasePrice
                });

                allocation.Batch.Quantity -= allocation.Quantity;
            }

            cashbackLines.Add(new CashbackLine(variantProduct[item.VariantId], line.Quantity, line.Price * line.Quantity * cashbackFactor));
        }

        db.Sales.Add(sale);

        await PostLedgerAsync(sale, warehouse.BranchId, debtAmount, cashbackLines, userId, shiftId, cancellationToken);

        sale.RaiseDomainEvent(new SaleCompletedEvent(sale.ReceiptToken, sale.BranchId, sale.CustomerId, sale.TotalAmount));

        if (hasOverride)
            audit.Add("priceOverride", "sales", null,
                request.Items.Where(i => i.UnitPrice is not null).Select(i => new { i.VariantId, i.UnitPrice }));

        await db.SaveChangesAsync(cancellationToken);

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
        RuleFor(x => x.PaidCash).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PaidCard).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PaidBonus).GreaterThanOrEqualTo(0);
    }
}
