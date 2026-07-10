using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Domain.Common.Exceptions;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Customers.Commands;

public record DeleteCustomerCommand(long Id) : ICommand<Unit>;

public sealed class DeleteCustomerCommandHandler(IApplicationDbContext db, IAuditService audit)
    : IRequestHandler<DeleteCustomerCommand, Unit>
{
    public async Task<Unit> Handle(DeleteCustomerCommand request, CancellationToken cancellationToken)
    {
        var customer = await db.Customers
            .Include(c => c.Accounts.Where(a => a.Type == AccountType.Debt))
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Customer not found.");

        if (customer.Accounts.Any(a => a.Balance != 0))
            throw new BusinessRuleException("Qarzi bor mijozni o'chirib bo'lmaydi.");

        customer.IsDeleted = true;
        audit.Add("delete", "customer", customer.Id, new { customer.FullName });
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
