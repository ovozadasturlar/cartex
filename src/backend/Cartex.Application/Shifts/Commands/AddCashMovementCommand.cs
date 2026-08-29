using FluentValidation;
using Cartex.Persistence;
using Cartex.Domain.Common;
using Cartex.Domain.Enums;
using Cartex.Application.Common.Finance;
using Cartex.Application.Common.Shifts;

namespace Cartex.Application.Shifts.Commands;

public record AddCashMovementCommand(decimal Amount, bool IsPayOut, string? Reason = null, long? ExpenseCategoryId = null) : ICommand<Unit>;

public sealed class AddCashMovementCommandHandler(IApplicationDbContext db, ICurrentUser currentUser, ILedgerService ledger, IShiftLock shiftLock, IAuditService audit) : IRequestHandler<AddCashMovementCommand, Unit>
{
    public async Task<Unit> Handle(AddCashMovementCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("Not authenticated.");
        var branchId = currentUser.DefaultBranchId ?? throw new BusinessRuleException("Filial aniqlanmadi.");

        var shift = await shiftLock.OpenAsync(userId, branchId, cancellationToken)
            ?? throw new BusinessRuleException("Ochiq smena yo'q.");

        var cash = request.IsPayOut
            ? await CashPayout.AccountAsync(ledger, branchId, request.Amount, cancellationToken)
            : await ledger.BranchAccountAsync(branchId, AccountType.Cash, cancellationToken);
        var transaction = request.IsPayOut
            ? await ledger.PostAsync(OperationType.CashOut, request.Amount, cash, null, userId, cancellationToken, shift.Id)
            : await ledger.PostAsync(OperationType.CashIn, request.Amount, null, cash, userId, cancellationToken, shift.Id);
        transaction.Description = request.Reason;
        if (request.IsPayOut) transaction.ExpenseCategoryId = request.ExpenseCategoryId;

        audit.Add(request.IsPayOut ? "cashout" : "cashin", "shifts", shift.Id,
            new { request.Amount, request.Reason });

        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed class AddCashMovementCommandValidator : AbstractValidator<AddCashMovementCommand>
{
    public AddCashMovementCommandValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}
