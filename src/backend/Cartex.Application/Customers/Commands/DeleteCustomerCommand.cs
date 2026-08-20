using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Customers.Commands;

public record DeleteCustomerCommand(long Id) : ICommand<Unit>;

public sealed class DeleteCustomerCommandHandler(IApplicationDbContext db, ICurrentUser currentUser, IAuditService audit)
    : IRequestHandler<DeleteCustomerCommand, Unit>
{
    public async Task<Unit> Handle(DeleteCustomerCommand request, CancellationToken cancellationToken)
    {
        var customer = await db.Customers
            .Include(c => c.Accounts)
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Customer not found.");

        if (!currentUser.HasPermission(AppPermissions.Customers.ViewAll) && customer.AssignedUserId != currentUser.UserId)
            throw new NotFoundException("Customer not found.");

        // QARZ-21: qarz va avans — haqiqiy pul, ochiq qolsa o'chirish uni ko'zdan yo'qotadi.
        // Bonus ataylab tashqarida: u pul emas va har keshbekdan keyin qoladi.
        if (customer.Accounts.Any(a => a.Balance != 0
            && a.Type is AccountType.Debt or AccountType.CustomerAdvance))
            throw new BusinessRuleException(
                "Hisob-kitobi yopilmagan mijozni o'chirib bo'lmaydi.", "customer_balance_open");

        customer.IsDeleted = true;
        audit.Add("delete", "customer", customer.Id, new { customer.FullName });
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
