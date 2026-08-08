using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Cartex.Domain.Common;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Application.Common.Finance;

using Unit = Cartex.Application.Common.Messaging.Unit;

namespace Cartex.Application.Suppliers.Commands;

public record PaySupplierDebtCommand(long SupplierId, decimal Amount, AccountType Method = AccountType.Cash, string? DebtCurrency = null, string? PayCurrency = null, long? SupplyId = null, string? IdempotencyKey = null) : ICommand<Unit>;

public sealed class PaySupplierDebtCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    ILedgerService ledger,
    ICurrencyService currency,
    ISettingsService settingsService,
    IAuditService audit) : IRequestHandler<PaySupplierDebtCommand, Unit>
{
    public async Task<Unit> Handle(PaySupplierDebtCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("Not authenticated.");

        long branchId;
        if (request.SupplyId is { } supplyId)
            branchId = await db.Supplies
                .Where(s => s.Id == supplyId && s.SupplierId == request.SupplierId)
                .Select(s => (long?)s.BranchId)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException("Ta'minot topilmadi.");
        else
            branchId = currentUser.DefaultBranchId ?? throw new BusinessRuleException("Foydalanuvchi filiali aniqlanmadi.");

        var idempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey.Trim();
        if (idempotencyKey is not null
            && await db.Transactions.AnyAsync(t => t.UserId == userId && t.IdempotencyKey == idempotencyKey, cancellationToken))
            return Unit.Value;

        var baseCode = await currency.BaseAsync(cancellationToken);
        var debtCurrency = request.DebtCurrency ?? baseCode;
        var payCurrency = request.PayCurrency ?? debtCurrency;

        await currency.EnsurePricingAllowedAsync(debtCurrency, cancellationToken);
        await currency.EnsurePricingAllowedAsync(payCurrency, cancellationToken);

        if (request.Method != AccountType.Cash && payCurrency != baseCode)
            throw new BusinessRuleException("Naqd bo'lmagan to'lov faqat bazaviy valyutada.");

        var debt = await ledger.SupplierAccountAsync(request.SupplierId, AccountType.Debt, cancellationToken, debtCurrency);

        var payRate = payCurrency == baseCode ? 1m : await currency.RateAsync(payCurrency, cancellationToken);
        var debtRate = debtCurrency == baseCode ? 1m : await currency.RateAsync(debtCurrency, cancellationToken);
        var debtReduce = payCurrency == debtCurrency
            ? request.Amount
            : Math.Round(request.Amount * payRate / debtRate, 2);

        var shiftId = await db.Shifts
            .Where(s => s.UserId == userId && s.BranchId == branchId && s.Status == ShiftStatus.Open)
            .Select(s => (long?)s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var policy = await settingsService.GetAsync<SalesPolicySettings>(SettingKeys.SalesPolicy, cancellationToken) ?? new SalesPolicySettings();
        if (request.Method == AccountType.Cash && shiftId is null && policy.ShiftPolicy != "Off")
            throw new BusinessRuleException("Naqd to'lov uchun ochiq smena talab qilinadi.");

        var branchAccount = await ledger.BranchAccountAsync(branchId, request.Method, cancellationToken, payCurrency);

        var opType = request.SupplyId is null ? OperationType.DebtPay : OperationType.SupplyPay;

        if (payCurrency == debtCurrency)
        {
            var tx = ledger.Post(opType, request.Amount, branchAccount, debt, userId, shiftId, debtRate);
            tx.SupplyId = request.SupplyId;
            tx.IdempotencyKey = idempotencyKey;
        }
        else
        {
            var tx = ledger.Post(opType, request.Amount, branchAccount, null, userId, shiftId, payRate);
            tx.SupplyId = request.SupplyId;
            tx.IdempotencyKey = idempotencyKey;
            ledger.Post(opType, debtReduce, null, debt, userId, shiftId, debtRate).SupplyId = request.SupplyId;
        }

        audit.Add("supplierpay", "suppliers", request.SupplierId, new { request.Amount });

        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed class PaySupplierDebtCommandValidator : AbstractValidator<PaySupplierDebtCommand>
{
    private static readonly AccountType[] PaymentAccounts =
        [AccountType.Cash, AccountType.Card, AccountType.Transfer, AccountType.Bank];

    public PaySupplierDebtCommandValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Method).Must(PaymentAccounts.Contains).WithMessage("To'lov turi noto'g'ri.");
    }
}
