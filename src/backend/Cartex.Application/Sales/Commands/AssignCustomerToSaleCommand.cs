using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Settings;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Unit = Cartex.Application.Common.Messaging.Unit;

namespace Cartex.Application.Sales.Commands;

public sealed record AssignCustomerToSaleCommand(long SaleId, long CustomerId) : ICommand<Unit>;

public sealed class AssignCustomerToSaleCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IAuditService audit) : IRequestHandler<AssignCustomerToSaleCommand, Unit>
{
    public async Task<Unit> Handle(AssignCustomerToSaleCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(AppPermissions.Sales.AssignCustomer))
            throw new ForbiddenException("Savdoga mijoz biriktirishga ruxsat yo'q.");

        var sale = await db.Sales
            .FromSqlInterpolated($"SELECT * FROM sales WHERE id = {request.SaleId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Sale not found.", "sale_not_found");

        if (!currentUser.CanAccessAllBranches && !currentUser.BranchIds.Contains(sale.BranchId))
            throw new NotFoundException("Sale not found.", "sale_not_found");

        if (sale.Status != SaleStatus.Completed)
            throw new BusinessRuleException("Faqat yakunlangan savdoga mijoz biriktirish mumkin.", "sale_not_completed");

        if (sale.CustomerId == request.CustomerId)
            return Unit.Value;

        if (sale.CustomerId is not null && (sale.PaidBonus > 0 || sale.DebtAmount > 0 || sale.RefundedBonus > 0 || sale.RefundedDebt > 0))
            throw new BusinessRuleException("Bonus yoki qarz bog'langan savdoni boshqa mijozga o'tkazib bo'lmaydi.", "sale_customer_locked");

        var customer = await db.Customers.Include(x => x.Party)
            .SingleOrDefaultAsync(x => x.Id == request.CustomerId, cancellationToken)
            ?? throw new NotFoundException("Customer not found.", "customer_not_found");

        var previousCustomerId = sale.CustomerId;
        sale.CustomerId = customer.Id;

        await db.SaveChangesAsync(cancellationToken);

        audit.SetOutcome("sale.customer_assigned", "sales", sale.Id, new
        {
            saleId = sale.Id,
            sale.ReceiptToken,
            previousCustomerId,
            newCustomerId = customer.Id,
            customerName = $"{customer.Party.FullName} {customer.LastName}".Trim()
        }, "Savdoga mijoz biriktirildi", sale.BranchId);

        return Unit.Value;
    }
}

public sealed class AssignCustomerToSaleCommandValidator : AbstractValidator<AssignCustomerToSaleCommand>
{
    public AssignCustomerToSaleCommandValidator()
    {
        RuleFor(x => x.SaleId).GreaterThan(0);
        RuleFor(x => x.CustomerId).GreaterThan(0);
    }
}
