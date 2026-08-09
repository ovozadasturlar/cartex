using FluentValidation;
using Cartex.Domain.Enums;
using Cartex.Application.Common.Finance;
using Cartex.Application.CustomerPayments.Commands;

namespace Cartex.Application.Customers.Commands;

public record RepayCustomerDebtCommand(long CustomerId, decimal Amount, bool ViaCard, string? DebtCurrency = null, string? PayCurrency = null, string? IdempotencyKey = null) : ICommand<Unit>;

public sealed class RepayCustomerDebtCommandHandler(
    ICurrencyService currency,
    ISender sender) : IRequestHandler<RepayCustomerDebtCommand, Unit>
{
    public async Task<Unit> Handle(RepayCustomerDebtCommand request, CancellationToken cancellationToken)
    {
        var baseCode = await currency.BaseAsync(cancellationToken);
        var debtCurrency = (request.DebtCurrency ?? baseCode).Trim().ToUpperInvariant();
        var payCurrency = (request.PayCurrency ?? debtCurrency).Trim().ToUpperInvariant();

        await currency.EnsureSalesAllowedAsync(debtCurrency, cancellationToken);
        await currency.EnsureSalesAllowedAsync(payCurrency, cancellationToken);

        var payRate = payCurrency == baseCode ? 1m : await currency.RateAsync(payCurrency, cancellationToken);
        var debtRate = debtCurrency == baseCode ? 1m : await currency.RateAsync(debtCurrency, cancellationToken);
        var debtReduce = payCurrency == debtCurrency
            ? request.Amount
            : Math.Round(request.Amount * payRate / debtRate, 2);

        await sender.Send(new CreateCustomerPaymentCommand(
            request.CustomerId,
            null,
            [new CustomerPaymentTenderInput(request.ViaCard ? PaymentMethod.Card : PaymentMethod.Cash, payCurrency, request.Amount)],
            [new CustomerPaymentAllocationInput(debtCurrency, debtReduce)],
            AutoAllocateDebt: false,
            IdempotencyKey: request.IdempotencyKey), cancellationToken);
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
