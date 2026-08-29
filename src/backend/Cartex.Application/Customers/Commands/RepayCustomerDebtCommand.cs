using FluentValidation;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Enums;
using Cartex.Application.Common.Finance;
using Cartex.Application.CustomerPayments.Commands;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Customers.Commands;

public record RepayCustomerDebtCommand(long CustomerId, decimal Amount, bool ViaCard, string? DebtCurrency = null, string? PayCurrency = null, string? IdempotencyKey = null, decimal WriteOff = 0, string? WriteOffReason = null) : ICommand<Unit>;

public sealed class RepayCustomerDebtCommandHandler(
    IApplicationDbContext db,
    ICurrencyService currency,
    ICurrentUser currentUser,
    ISender sender) : IRequestHandler<RepayCustomerDebtCommand, Unit>
{
    public async Task<Unit> Handle(RepayCustomerDebtCommand request, CancellationToken cancellationToken)
    {
        if (request.WriteOff > 0 && !currentUser.HasPermission(AppPermissions.CustomerPayments.WriteOffDebt))
            throw new ForbiddenException("Mijoz qarzini kechirishga ruxsat yo'q.");

        var baseCode = await currency.BaseAsync(cancellationToken);
        var debtCurrency = (request.DebtCurrency ?? baseCode).Trim().ToUpperInvariant();
        var payCurrency = (request.PayCurrency ?? debtCurrency).Trim().ToUpperInvariant();
        if (request.WriteOff > 0 && debtCurrency != baseCode)
            throw new BusinessRuleException("Kechirim faqat bazaviy valyutada.", "write_off_base_currency_only");

        await currency.EnsureSalesAllowedAsync(debtCurrency, cancellationToken);
        await currency.EnsureSalesAllowedAsync(payCurrency, cancellationToken);

        var payRate = payCurrency == baseCode ? 1m : await currency.RateAsync(payCurrency, cancellationToken);
        var debtRate = debtCurrency == baseCode ? 1m : await currency.RateAsync(debtCurrency, cancellationToken);
        var debtReduce = payCurrency == debtCurrency
            ? request.Amount
            : Math.Round(request.Amount * payRate / debtRate, 2);

        // QARZ-03: taqsimot mavjud qarzdan oshmaydi — ortiqcha summa avansga tushadi.
        var debtBalance = await db.Accounts
            .Where(x => x.CustomerId == request.CustomerId && x.Type == AccountType.Debt
                && x.Currency == debtCurrency && x.Balance > 0)
            .SumAsync(x => x.Balance, cancellationToken);
        var allocated = Math.Min(debtReduce, debtBalance);

        await sender.Send(new CreateCustomerPaymentCommand(
            request.CustomerId,
            null,
            request.Amount > 0
                ? [new CustomerPaymentTenderInput(request.ViaCard ? PaymentMethod.Card : PaymentMethod.Cash, payCurrency, request.Amount)]
                : [],
            allocated > 0 ? [new CustomerPaymentAllocationInput(debtCurrency, allocated)] : [],
            AutoAllocateDebt: false,
            IdempotencyKey: request.IdempotencyKey,
            WriteOffAmount: request.WriteOff,
            WriteOffReason: request.WriteOffReason), cancellationToken);
        return Unit.Value;
    }
}

public sealed class RepayCustomerDebtCommandValidator : AbstractValidator<RepayCustomerDebtCommand>
{
    public RepayCustomerDebtCommandValidator()
    {
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.WriteOff).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Amount).Must((cmd, amount) => amount + cmd.WriteOff > 0)
            .WithMessage("To'lov yoki kechirim summasi bo'lishi kerak.");
        RuleFor(x => x.WriteOffReason).NotEmpty().When(x => x.WriteOff > 0);
    }
}
