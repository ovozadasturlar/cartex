using Cartex.Application.Common.Messaging;
using FluentValidation;
using Cartex.Domain.Common;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Application.Common.Finance;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Customers.Commands;

public record GiveCustomerBonusCommand(long CustomerId, decimal Amount, string? Note) : ICommand<Unit>;

public sealed class GiveCustomerBonusCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    ILedgerService ledger,
    IAuditService audit) : IRequestHandler<GiveCustomerBonusCommand, Unit>
{
    public async Task<Unit> Handle(GiveCustomerBonusCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("Not authenticated.");

        var exists = await db.Customers.AnyAsync(c => c.Id == request.CustomerId, cancellationToken);
        if (!exists)
            throw new NotFoundException("Customer not found.");

        var bonus = await ledger.CustomerAccountAsync(request.CustomerId, AccountType.Bonus, cancellationToken);
        ledger.Post(OperationType.Cashback, request.Amount, null, bonus, userId);

        audit.Add("bonus", "customers", request.CustomerId, new { request.Amount, request.Note });

        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed class GiveCustomerBonusCommandValidator : AbstractValidator<GiveCustomerBonusCommand>
{
    public GiveCustomerBonusCommandValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}
