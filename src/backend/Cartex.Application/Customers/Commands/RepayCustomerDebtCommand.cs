using MediatR;
using FluentValidation;
using Cartex.Domain.Common;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Application.Common.Finance;

namespace Cartex.Application.Customers.Commands;

public record RepayCustomerDebtCommand(long CustomerId, decimal Amount, bool ViaCard) : ICommand<Unit>;

public sealed class RepayCustomerDebtCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    ILedgerService ledger) : IRequestHandler<RepayCustomerDebtCommand, Unit>
{
    public async Task<Unit> Handle(RepayCustomerDebtCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("Not authenticated.");
        var branchId = currentUser.DefaultBranchId ?? throw new BusinessRuleException("Foydalanuvchi filiali aniqlanmadi.");

        var debt = await ledger.FindCustomerAccountAsync(request.CustomerId, AccountType.Debt, cancellationToken)
            ?? throw new BusinessRuleException("Mijozda qarz mavjud emas.");

        if (request.Amount > debt.Balance)
            throw new BusinessRuleException("To'lov summasi qarzdan oshib ketdi.");

        var branchAccount = await ledger.BranchAccountAsync(branchId, request.ViaCard ? AccountType.Card : AccountType.Cash, cancellationToken);
        ledger.Post(OperationType.DebtPay, request.Amount, debt, branchAccount, userId);

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
