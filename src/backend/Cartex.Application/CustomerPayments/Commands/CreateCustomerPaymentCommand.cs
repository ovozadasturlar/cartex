using Cartex.Application.Common.Documents;
using Cartex.Application.Common.Finance;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Application.Common.Partners;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Shared.Models.Customers;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.CustomerPayments.Commands;

public sealed record CustomerPaymentTenderInput(PaymentMethod Method, string Currency, decimal Amount);
public sealed record CustomerPaymentAllocationInput(string Currency, decimal Amount, long? SaleId = null,
    CustomerPaymentAllocationKind Kind = CustomerPaymentAllocationKind.Payment);

public sealed record CreateCustomerPaymentCommand(
    long CustomerId,
    long? BranchId,
    List<CustomerPaymentTenderInput> Tenders,
    List<CustomerPaymentAllocationInput>? Allocations = null,
    bool AutoAllocateDebt = true,
    DateOnly? BusinessDate = null,
    string? Note = null,
    string? IdempotencyKey = null,
    decimal WriteOffAmount = 0,
    string? WriteOffReason = null) : ICommand<CustomerPaymentCreatedDto>;

public sealed class CreateCustomerPaymentCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    ILedgerService ledger,
    ICurrencyService currency,
    ISettingsService settings,
    IPartnerRewardService partnerRewards,
    IAuditService audit) : IRequestHandler<CreateCustomerPaymentCommand, CustomerPaymentCreatedDto>
{
    public async Task<CustomerPaymentCreatedDto> Handle(CreateCustomerPaymentCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("Not authenticated.");
        if (!currentUser.HasPermission(AppPermissions.CustomerPayments.Create)
            && !currentUser.HasPermission(AppPermissions.Customers.ReceivePayment))
            throw new ForbiddenException("Mijoz to'lovini qabul qilishga ruxsat yo'q.");

        // Qobiliyat mijozdan oldin tekshiriladi: kechira olmaydigan foydalanuvchi mijoz
        // haqida umuman so'roq ham qilmasin.
        if (request.WriteOffAmount > 0)
        {
            if (!currentUser.HasPermission(AppPermissions.CustomerPayments.WriteOffDebt))
                throw new ForbiddenException("Mijoz qarzini kechirishga ruxsat yo'q.");
            if (string.IsNullOrWhiteSpace(request.WriteOffReason))
                throw new BusinessRuleException("Kechirim sababi ko'rsatilishi shart.", "write_off_reason_required");
        }

        var branchId = request.BranchId ?? currentUser.DefaultBranchId
            ?? throw new BusinessRuleException("Foydalanuvchi filiali aniqlanmadi.");
        if (!currentUser.CanAccessAllBranches && !currentUser.BranchIds.Contains(branchId))
            throw new ForbiddenException("Bu filialga ruxsat yo'q.");

        var idempotencyKey = NormalizeOptional(request.IdempotencyKey);
        if (idempotencyKey is not null)
        {
            var existing = await db.CustomerPaymentDocuments
                .Where(x => x.BranchId == branchId && x.IdempotencyKey == idempotencyKey)
                .Select(x => new CustomerPaymentCreatedDto(
                    x.Id, x.DocumentNumber, x.TotalBaseAmount, x.AllocatedBaseAmount, x.AdvanceBaseAmount))
                .FirstOrDefaultAsync(cancellationToken);
            if (existing is not null)
                return existing;
        }

        var customer = await db.Customers.FirstOrDefaultAsync(x => x.Id == request.CustomerId, cancellationToken)
            ?? throw new NotFoundException("Customer not found.", "customer_not_found");
        if (!currentUser.HasPermission(AppPermissions.Customers.ViewAll)
            && customer.AssignedUserId != currentUser.UserId)
            throw new NotFoundException("Customer not found.", "customer_not_found");

        var baseCode = await currency.BaseAsync(cancellationToken);
        var normalizedTenders = request.Tenders.Select(x => new CustomerPaymentTenderInput(
            x.Method, NormalizeCurrency(x.Currency, baseCode), x.Amount)).ToList();
        if (normalizedTenders.Any(x => x.Method == PaymentMethod.Bonus))
            throw new BusinessRuleException("Bonus mijoz to'lovi sifatida qabul qilinmaydi.", "unsupported_payment_method");

        // Baza valyuta doim kerak: sof kechirimda bironta tender ham bo'lmaydi, lekin
        // taqsimot baribir kurs bo'yicha hisoblanadi.
        var distinctCodes = normalizedTenders.Select(x => x.Currency)
            .Concat(request.Allocations?.Select(x => NormalizeCurrency(x.Currency, baseCode)) ?? [])
            .Append(baseCode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var rates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var code in distinctCodes)
        {
            await currency.EnsureSalesAllowedAsync(code, cancellationToken);
            rates[code] = await currency.RateAsync(code, cancellationToken);
        }

        var tenderRows = normalizedTenders.Select(x => new CustomerPaymentTender
        {
            Method = x.Method,
            Currency = x.Currency,
            Amount = x.Amount,
            Rate = rates[x.Currency],
            AmountBase = Math.Round(x.Amount * rates[x.Currency], 2)
        }).ToList();
        var totalBase = tenderRows.Sum(x => x.AmountBase);
        // Naqdsiz kechirim ham to'liq hujjat: qarz yopiladi, lekin pul olinmaydi.
        var writeOffBase = Math.Round(Math.Max(0, request.WriteOffAmount), 2);
        if (totalBase <= 0 && writeOffBase <= 0)
            throw new BusinessRuleException("To'lov summasi 0 dan katta bo'lishi kerak.");

        var shiftId = await db.Shifts
            .Where(x => x.UserId == userId && x.BranchId == branchId && x.Status == ShiftStatus.Open)
            .Select(x => (long?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        var policy = await settings.GetAsync<SalesPolicySettings>(SettingKeys.SalesPolicy, cancellationToken)
            ?? new SalesPolicySettings();
        if (tenderRows.Any(x => x.Method == PaymentMethod.Cash) && shiftId is null && policy.ShiftPolicy != "Off"
            && !await db.Warehouses.AnyAsync(x => x.AssignedUserId == userId, cancellationToken))
            throw new BusinessRuleException("Naqd to'lov uchun ochiq smena talab qilinadi.");

        if (writeOffBase > 0)
        {
            if (policy.MaxDebtWriteOffAmount > 0 && writeOffBase > policy.MaxDebtWriteOffAmount)
                throw new BusinessRuleException(
                    $"Kechirim {policy.MaxDebtWriteOffAmount:N0} dan osha olmaydi.", "write_off_limit_exceeded");
            // Chegara bazasi — shu hujjat yopayotgan summa: to'langan + kechirilgan.
            if (policy.MaxDebtWriteOffPercent > 0
                && writeOffBase > (totalBase + writeOffBase) * policy.MaxDebtWriteOffPercent / 100)
                throw new BusinessRuleException(
                    $"Kechirim {policy.MaxDebtWriteOffPercent}% dan osha olmaydi.", "write_off_limit_exceeded");
        }

        var debtRows = await db.Accounts
            .Where(x => x.CustomerId == request.CustomerId && x.Type == AccountType.Debt && x.Balance > 0)
            .OrderByDescending(x => x.Currency == baseCode)
            .ThenBy(x => x.CreatedAt)
            .Select(x => new { x.Currency, x.Balance })
            .ToListAsync(cancellationToken);
        var remainingDebt = debtRows.ToDictionary(x => x.Currency, x => x.Balance, StringComparer.OrdinalIgnoreCase);

        var allocations = new List<CustomerPaymentAllocationInput>();
        foreach (var row in request.Allocations ?? [])
        {
            var code = NormalizeCurrency(row.Currency, baseCode);
            if (!remainingDebt.TryGetValue(code, out var balance) || row.Amount > balance)
                throw new BusinessRuleException($"{code} bo'yicha qarz yetarli emas.", "payment_exceeds_debt");
            remainingDebt[code] = balance - row.Amount;
            allocations.Add(new CustomerPaymentAllocationInput(code, row.Amount, row.SaleId));
        }

        var explicitlyAllocatedBase = allocations.Sum(x => Math.Round(x.Amount * rates[x.Currency], 2));
        if (explicitlyAllocatedBase > totalBase)
            throw new BusinessRuleException("Qarzga taqsimlangan summa to'lovdan oshib ketdi.", "allocation_exceeds_payment");

        if (request.AutoAllocateDebt || writeOffBase > 0)
        {
            var debtSales = await db.Sales
                .Where(x => x.CustomerId == request.CustomerId && x.DebtAmount > x.RefundedDebt)
                .OrderBy(x => x.DebtDueDate == null)
                .ThenBy(x => x.DebtDueDate)
                .ThenBy(x => x.CreatedAt)
                .Select(x => new { x.Id, x.DebtAmount, x.RefundedDebt, x.DebtCurrency, x.DebtRate })
                .ToListAsync(cancellationToken);
            var debtSaleIds = debtSales.Select(x => x.Id).ToList();
            var priorBySale = debtSaleIds.Count == 0
                ? new Dictionary<long, decimal>()
                : await db.CustomerPaymentAllocations
                    .Where(x => x.SaleId != null
                                && debtSaleIds.Contains(x.SaleId.Value)
                                && x.Document.Status == BusinessDocumentStatus.Posted)
                    .GroupBy(x => x.SaleId!.Value)
                    .Select(x => new { SaleId = x.Key, Amount = x.Sum(a => a.Amount) })
                    .ToDictionaryAsync(x => x.SaleId, x => x.Amount, cancellationToken);

            // Kechirim birinchi bo'lib, eng eski muddatdagi qarzdan boshlab qo'llanadi —
            // uning maqsadi aynan eng eskirgan qarzni yopish.
            var unplacedWriteOff = await AllocateAsync(writeOffBase, CustomerPaymentAllocationKind.WriteOff);
            if (unplacedWriteOff > 0)
                throw new BusinessRuleException("Kechirim mijoz qarzidan oshib ketdi.", "write_off_exceeds_debt");

            if (request.AutoAllocateDebt)
                await AllocateAsync(totalBase - explicitlyAllocatedBase, CustomerPaymentAllocationKind.Payment);

            // Allocate to concrete invoices first so statements remain reconcilable.
            // Any opening/legacy balance which has no source invoice is handled below.
            async Task<decimal> AllocateAsync(decimal budgetBase, CustomerPaymentAllocationKind kind)
            {
                foreach (var sale in debtSales)
                {
                    if (budgetBase <= 0) break;
                    var code = NormalizeCurrency(sale.DebtCurrency, baseCode);
                    var accountRemaining = remainingDebt.GetValueOrDefault(code);
                    if (accountRemaining <= 0) continue;
                    var rate = sale.DebtRate <= 0 ? await GetRateAsync(code) : sale.DebtRate;
                    var originalNative = Math.Round(
                        Math.Max(0, sale.DebtAmount - sale.RefundedDebt) / rate, 4);
                    var currentExplicit = allocations
                        .Where(x => x.SaleId == sale.Id && x.Currency == code)
                        .Sum(x => x.Amount);
                    var saleRemaining = Math.Max(0,
                        originalNative - priorBySale.GetValueOrDefault(sale.Id) - currentExplicit);
                    if (saleRemaining <= 0) continue;

                    var affordable = Math.Floor(budgetBase / rate * 10_000m) / 10_000m;
                    var amount = Math.Min(Math.Min(accountRemaining, saleRemaining), affordable);
                    var amountBase = Math.Round(amount * rate, 2);
                    if (amount <= 0 || amountBase <= 0) continue;
                    allocations.Add(new CustomerPaymentAllocationInput(code, amount, sale.Id, kind));
                    remainingDebt[code] -= amount;
                    budgetBase -= amountBase;
                }

                foreach (var row in debtRows.Where(x => remainingDebt.GetValueOrDefault(x.Currency) > 0))
                {
                    if (budgetBase <= 0) break;
                    var rate = await GetRateAsync(row.Currency);
                    var balance = remainingDebt[row.Currency];
                    var amount = Math.Min(balance, Math.Floor(budgetBase / rate * 10_000m) / 10_000m);
                    var amountBase = Math.Round(amount * rate, 2);
                    if (amount <= 0 || amountBase <= 0) continue;
                    allocations.Add(new CustomerPaymentAllocationInput(row.Currency, amount, null, kind));
                    remainingDebt[row.Currency] -= amount;
                    budgetBase -= amountBase;
                }

                return budgetBase;
            }

            async Task<decimal> GetRateAsync(string code)
            {
                if (rates.TryGetValue(code, out var cached)) return cached;
                await currency.EnsureSalesAllowedAsync(code, cancellationToken);
                var resolved = await currency.RateAsync(code, cancellationToken);
                rates[code] = resolved;
                return resolved;
            }
        }

        await ValidateSaleAllocationsAsync(request.CustomerId, allocations, cancellationToken);

        var businessDate = request.BusinessDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var document = new CustomerPaymentDocument
        {
            BranchId = branchId,
            CustomerId = request.CustomerId,
            UserId = userId,
            DocumentNumber = await DocumentNumbers.NextAsync(db, "PAY", businessDate, cancellationToken),
            BusinessDate = businessDate,
            Note = NormalizeOptional(request.Note),
            IdempotencyKey = idempotencyKey,
            TotalBaseAmount = totalBase,
            Tenders = tenderRows
        };

        foreach (var tender in tenderRows)
        {
            var account = await ledger.BranchAccountAsync(branchId, AccountTypeFor(tender.Method), cancellationToken, tender.Currency);
            var transaction = ledger.Post(OperationType.CustomerPayment, tender.Amount, null, account, userId, shiftId, tender.Rate);
            transaction.CustomerPaymentDocument = document;
            transaction.Description = document.DocumentNumber;
        }

        decimal allocatedBase = 0;
        decimal writtenOffBase = 0;
        foreach (var allocation in allocations)
        {
            var rate = rates[allocation.Currency];
            var amountBase = Math.Round(allocation.Amount * rate, 2);
            var debt = await ledger.FindCustomerAccountAsync(
                request.CustomerId, AccountType.Debt, cancellationToken, allocation.Currency)
                ?? throw new BusinessRuleException($"{allocation.Currency} bo'yicha qarz mavjud emas.");
            if (allocation.Amount > debt.Balance)
                throw new BusinessRuleException($"{allocation.Currency} bo'yicha qarz yetarli emas.", "payment_exceeds_debt");

            var row = new CustomerPaymentAllocation
            {
                SaleId = allocation.SaleId,
                Currency = allocation.Currency,
                Amount = allocation.Amount,
                Rate = rate,
                AmountBase = amountBase,
                Kind = allocation.Kind
            };
            document.Allocations.Add(row);
            var operation = allocation.Kind == CustomerPaymentAllocationKind.WriteOff
                ? OperationType.DebtWriteOff
                : OperationType.DebtPay;
            var transaction = ledger.Post(operation, allocation.Amount, debt, null, userId, shiftId, rate);
            transaction.CustomerPaymentDocument = document;
            transaction.SaleId = allocation.SaleId;
            transaction.Description = document.DocumentNumber;
            if (allocation.Kind == CustomerPaymentAllocationKind.WriteOff) writtenOffBase += amountBase;
            else allocatedBase += amountBase;
        }

        document.AllocatedBaseAmount = allocatedBase;
        document.WriteOffBaseAmount = writtenOffBase;
        document.WriteOffReason = writtenOffBase > 0 ? NormalizeOptional(request.WriteOffReason) : null;
        // Kechirim pul emas — ortiqcha avansga faqat haqiqatan olingan puldan hisoblanadi.
        document.AdvanceBaseAmount = Math.Max(0, totalBase - allocatedBase);
        if (document.AdvanceBaseAmount > 0)
        {
            var advance = await ledger.CustomerAccountAsync(
                request.CustomerId, AccountType.CustomerAdvance, cancellationToken, baseCode);
            var transaction = ledger.Post(
                OperationType.CustomerAdvance, document.AdvanceBaseAmount, null, advance, userId, shiftId);
            transaction.CustomerPaymentDocument = document;
            transaction.Description = document.DocumentNumber;
        }

        document.BalanceAfterBase = await CustomerBalance.NetAsync(db, currency, request.CustomerId, cancellationToken);

        db.CustomerPaymentDocuments.Add(document);
        await partnerRewards.AccruePaymentAsync(document, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        audit.SetOutcome("customer.payment_received", "customer_payment_documents", document.Id, new
        {
            document.DocumentNumber,
            document.CustomerId,
            document.TotalBaseAmount,
            document.AllocatedBaseAmount,
            document.AdvanceBaseAmount,
            tenders = document.Tenders.Select(x => new { x.Method, x.Currency, x.Amount, x.Rate, x.AmountBase }),
            allocations = document.Allocations.Select(x => new { x.SaleId, x.Currency, x.Amount, x.Rate, x.AmountBase })
        }, "Mijozdan to'lov qabul qilindi", branchId);

        return new CustomerPaymentCreatedDto(document.Id, document.DocumentNumber,
            document.TotalBaseAmount, document.AllocatedBaseAmount, document.AdvanceBaseAmount);
    }

    private async Task ValidateSaleAllocationsAsync(
        long customerId,
        IReadOnlyCollection<CustomerPaymentAllocationInput> allocations,
        CancellationToken cancellationToken)
    {
        var saleIds = allocations.Where(x => x.SaleId.HasValue).Select(x => x.SaleId!.Value).Distinct().ToList();
        if (saleIds.Count == 0) return;

        var sales = await db.Sales.Where(x => saleIds.Contains(x.Id))
            .Select(x => new { x.Id, x.CustomerId, x.DebtAmount, x.RefundedDebt, x.DebtCurrency, x.DebtRate })
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var alreadyAllocated = await db.CustomerPaymentAllocations
            .Where(x => x.SaleId != null && saleIds.Contains(x.SaleId.Value)
                && x.Document.Status == BusinessDocumentStatus.Posted)
            .GroupBy(x => new { SaleId = x.SaleId!.Value, x.Currency })
            .Select(x => new { x.Key.SaleId, x.Key.Currency, Amount = x.Sum(a => a.Amount) })
            .ToListAsync(cancellationToken);

        foreach (var group in allocations.Where(x => x.SaleId.HasValue).GroupBy(x => x.SaleId!.Value))
        {
            if (!sales.TryGetValue(group.Key, out var sale) || sale.CustomerId != customerId)
                throw new BusinessRuleException("To'lov taqsimlanayotgan savdo mijozga tegishli emas.", "invalid_payment_allocation");
            if (group.Any(x => x.Currency != sale.DebtCurrency))
                throw new BusinessRuleException("Savdo qarzi valyutasi va taqsimot valyutasi mos emas.", "invalid_payment_allocation");

            var remainingAfterReturns = Math.Max(0, sale.DebtAmount - sale.RefundedDebt);
            var originalDebt = sale.DebtRate == 0
                ? remainingAfterReturns
                : Math.Round(remainingAfterReturns / sale.DebtRate, 4);
            var prior = alreadyAllocated.Where(x => x.SaleId == group.Key && x.Currency == sale.DebtCurrency).Sum(x => x.Amount);
            if (group.Sum(x => x.Amount) > originalDebt - prior)
                throw new BusinessRuleException("Savdo bo'yicha qolgan qarzdan ortiq summa taqsimlandi.", "allocation_exceeds_sale_debt");
        }
    }

    private static AccountType AccountTypeFor(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => AccountType.Cash,
        PaymentMethod.Card => AccountType.Card,
        PaymentMethod.Transfer => AccountType.Transfer,
        PaymentMethod.Bank => AccountType.Bank,
        _ => throw new BusinessRuleException("Qo'llab-quvvatlanmaydigan to'lov turi.", "unsupported_payment_method")
    };

    private static string NormalizeCurrency(string? code, string baseCode) =>
        string.IsNullOrWhiteSpace(code) ? baseCode : code.Trim().ToUpperInvariant();

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class CreateCustomerPaymentCommandValidator : AbstractValidator<CreateCustomerPaymentCommand>
{
    public CreateCustomerPaymentCommandValidator()
    {
        RuleFor(x => x.CustomerId).GreaterThan(0);
        // Sof kechirimda to'lov qatori bo'lmaydi (QARZ-16).
        RuleFor(x => x.Tenders).Must((cmd, tenders) => tenders.Count > 0 || cmd.WriteOffAmount > 0)
            .WithMessage("To'lov qatori yoki kechirim summasi bo'lishi kerak.");
        RuleFor(x => x.Tenders).Must(x => x.Count <= 20);
        RuleFor(x => x.WriteOffAmount).GreaterThanOrEqualTo(0);
        RuleForEach(x => x.Tenders).ChildRules(row =>
        {
            row.RuleFor(x => x.Amount).GreaterThan(0);
            row.RuleFor(x => x.Currency).MaximumLength(3);
        });
        RuleFor(x => x.Allocations).Must(x => x is null || x.Count <= 100);
        RuleForEach(x => x.Allocations!).ChildRules(row =>
        {
            row.RuleFor(x => x.Amount).GreaterThan(0);
            row.RuleFor(x => x.Currency).MaximumLength(3);
        }).When(x => x.Allocations is not null);
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.IdempotencyKey).MaximumLength(128);
    }
}
