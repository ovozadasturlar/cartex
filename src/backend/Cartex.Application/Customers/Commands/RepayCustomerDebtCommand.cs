using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Cartex.Domain.Common;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Application.Common.Finance;

namespace Cartex.Application.Customers.Commands;

public record RepayCustomerDebtCommand(long CustomerId, decimal Amount, bool ViaCard, string? DebtCurrency = null, string? PayCurrency = null, string? IdempotencyKey = null) : ICommand<Unit>;

public sealed class RepayCustomerDebtCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    ILedgerService ledger,
    ICurrencyService currency,
    ISettingsService settingsService,
    IAuditService audit) : IRequestHandler<RepayCustomerDebtCommand, Unit>
{
    public async Task<Unit> Handle(RepayCustomerDebtCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("Not authenticated.");
        var branchId = currentUser.DefaultBranchId ?? throw new BusinessRuleException("Foydalanuvchi filiali aniqlanmadi.");

        var idempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey.Trim();
        if (idempotencyKey is not null
            && await db.Transactions.AnyAsync(t => t.UserId == userId && t.IdempotencyKey == idempotencyKey, cancellationToken))
            return Unit.Value;

        var baseCode = await currency.BaseAsync(cancellationToken);
        var debtCurrency = request.DebtCurrency ?? baseCode;
        var payCurrency = request.PayCurrency ?? debtCurrency;

        await currency.EnsureSalesAllowedAsync(debtCurrency, cancellationToken);
        await currency.EnsureSalesAllowedAsync(payCurrency, cancellationToken);

        if (request.ViaCard && payCurrency != baseCode)
            throw new BusinessRuleException("Karta to'lovi faqat bazaviy valyutada.");

        var debt = await ledger.FindCustomerAccountAsync(request.CustomerId, AccountType.Debt, cancellationToken, debtCurrency)
            ?? throw new BusinessRuleException("Mijozda qarz mavjud emas.");

        var payRate = payCurrency == baseCode ? 1m : await currency.RateAsync(payCurrency, cancellationToken);
        var debtRate = debtCurrency == baseCode ? 1m : await currency.RateAsync(debtCurrency, cancellationToken);
        var debtReduce = payCurrency == debtCurrency
            ? request.Amount
            : Math.Round(request.Amount * payRate / debtRate, 2);

        if (debt.Balance <= 0)
            throw new BusinessRuleException("Mijozda qarz yo'q.");

        if (debtReduce > debt.Balance)
            throw new BusinessRuleException("To'lov summasi qarzdan oshib ketdi.");

        var shiftId = await db.Shifts
            .Where(s => s.UserId == userId && s.BranchId == branchId && s.Status == ShiftStatus.Open)
            .Select(s => (long?)s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var policy = await settingsService.GetAsync<SalesPolicySettings>(SettingKeys.SalesPolicy, cancellationToken) ?? new SalesPolicySettings();
        if (!request.ViaCard && shiftId is null && policy.ShiftPolicy != "Off"
            && !await db.Warehouses.AnyAsync(w => w.AssignedUserId == userId, cancellationToken))
            throw new BusinessRuleException("Naqd to'lov uchun ochiq smena talab qilinadi.");

        var branchAccount = await ledger.BranchAccountAsync(branchId, request.ViaCard ? AccountType.Card : AccountType.Cash, cancellationToken, payCurrency);

        if (payCurrency == debtCurrency)
        {
            var tx = ledger.Post(OperationType.DebtPay, request.Amount, debt, branchAccount, userId, shiftId, debtRate);
            tx.IdempotencyKey = idempotencyKey;
        }
        else
        {
            var tx = ledger.Post(OperationType.DebtPay, request.Amount, null, branchAccount, userId, shiftId, payRate);
            tx.IdempotencyKey = idempotencyKey;
            ledger.Post(OperationType.DebtPay, debtReduce, debt, null, userId, shiftId, debtRate);
        }

        audit.Add("debtpay", "customers", request.CustomerId, new { request.Amount, payCurrency, debtCurrency });

        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed class RepayCustomerDebtCommandValidator : AbstractValidator<RepayCustomerDebtCommand>
{
    public RepayCustomerDebtCommandValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}
