using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Customers.Commands;

public record DeleteCustomerCommand(long Id) : ICommand<Unit>;

public sealed class DeleteCustomerCommandHandler(
    IApplicationDbContext db,
    CustomerOpeningBalance opening,
    ICurrentUser currentUser,
    IAuditService audit) : IRequestHandler<DeleteCustomerCommand, Unit>
{
    public async Task<Unit> Handle(DeleteCustomerCommand request, CancellationToken cancellationToken)
    {
        var customer = await db.Customers
            .Include(c => c.Accounts).Include(c => c.Party)
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Customer not found.");

        if (!currentUser.HasPermission(AppPermissions.Customers.ViewAll) && customer.AssignedUserId != currentUser.UserId)
            throw new NotFoundException("Customer not found.");

        // QARZ-24: hali hisob-kitobga kiradigan amal bo'lmagan mijoz - kiritish xatosi. Uning
        // qoldig'i savdo natijasi emas, o'sha xatoning o'zi, shuning uchun boshlang'ich yozuv
        // bilan birga olib tashlanadi - aks holda qarz umumiy hisobda osilib qolardi.
        // Tugallanmagan savat va navbat ham u bilan birga yopiladi: ular pul harakati emas.
        var untouched = await opening.IsUntouchedForWriteAsync(customer.Id, cancellationToken);
        var openingBalance = 0m;
        var carts = 0;
        if (untouched)
        {
            openingBalance = await opening.ClearAsync(customer.Id, cancellationToken);
            var now = DateTime.UtcNow;
            carts = await db.Carts.Where(x => x.CustomerId == customer.Id)
                .ExecuteUpdateAsync(x => x
                    .SetProperty(p => p.IsDeleted, true)
                    .SetProperty(p => p.DeletedAt, now), cancellationToken);
        }
        // QARZ-21: qarz va avans - haqiqiy pul, ochiq qolsa o'chirish uni ko'zdan yo'qotadi.
        // Bonus ataylab tashqarida: u pul emas va har keshbekdan keyin qoladi.
        else if (customer.Accounts.Any(a => a.Balance != 0
            && a.Type is AccountType.Debt or AccountType.CustomerAdvance))
        {
            throw new BusinessRuleException(
                "Hisob-kitobi yopilmagan mijozni o'chirib bo'lmaydi.", "customer_balance_open");
        }

        customer.IsDeleted = true;
        audit.Add("delete", "customer", customer.Id,
            new { customer.Party.FullName, Untouched = untouched, OpeningBalance = openingBalance, Carts = carts });
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
