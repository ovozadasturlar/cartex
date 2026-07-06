using Cartex.Application.Common.Messaging;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Cartex.Domain.Common;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Application.Common.Finance;

namespace Cartex.Application.Customers.Commands;

public record RepayCustomerDebtCommand(long CustomerId, decimal Amount, bool ViaCard, string? DebtCurrency = null, string? PayCurrency = null) : ICommand<Unit>;

public sealed class RepayCustomerDebtCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    ILedgerService ledger,
    ICurrencyService currency,
    IAuditService audit) : IRequestHandler<RepayCustomerDebtCommand, Unit>
{
    public async Task<Unit> Handle(RepayCustomerDebtCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("Not authenticated.");
        var branchId = currentUser.DefaultBranchId ?? throw new BusinessRuleException("Foydalanuvchi filiali aniqlanmadi.");

        var baseCode = await currency.BaseAsync(cancellationToken);
        var debtCurrency = request.DebtCurrency ?? baseCode;
        var payCurrency = request.PayCurrency ?? debtCurrency;

        if ((debtCurrency != baseCode || payCurrency != baseCode) && !await currency.IsMulticurrencyAsync(cancellationToken))
            throw new BusinessRuleException("Ko'p valyuta rejimi o'chirilgan.");

        if (request.ViaCard && payCurrency != baseCode)
            throw new BusinessRuleException("Karta to'lovi faqat bazaviy valyutada.");

        var debt = await ledger.FindCustomerAccountAsync(request.CustomerId, AccountType.Debt, cancellationToken, debtCurrency)
            ?? throw new BusinessRuleException("Mijozda qarz mavjud emas.");

        var payRate = payCurrency == baseCode ? 1m : await currency.RateAsync(payCurrency, cancellationToken);
        var debtRate = debtCurrency == baseCode ? 1m : await currency.RateAsync(debtCurrency, cancellationToken);
        var debtReduce = payCurrency == debtCurrency
            ? request.Amount
            : Math.Round(request.Amount * payRate / debtRate, 2);

        if (debtReduce > debt.Balance)
            throw new BusinessRuleException("To'lov summasi qarzdan oshib ketdi.");

        var shiftId = await db.Shifts
            .Where(s => s.UserId == userId && s.BranchId == branchId && s.Status == ShiftStatus.Open)
            .Select(s => (long?)s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (!request.ViaCard && shiftId is null)
            throw new BusinessRuleException("Naqd to'lov uchun ochiq smena talab qilinadi.");

        var branchAccount = await ledger.BranchAccountAsync(branchId, request.ViaCard ? AccountType.Card : AccountType.Cash, cancellationToken, payCurrency);

        if (payCurrency == debtCurrency)
        {
            ledger.Post(OperationType.DebtPay, request.Amount, debt, branchAccount, userId, shiftId, debtRate);
        }
        else
        {
            ledger.Post(OperationType.DebtPay, request.Amount, null, branchAccount, userId, shiftId, payRate);
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
