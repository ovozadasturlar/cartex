using Cartex.Domain.Enums;
using Cartex.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

using Unit = Cartex.Application.Common.Messaging.Unit;

namespace Cartex.Application.Suppliers.Commands;

public record AttachSupplierPaymentsCommand(long SupplyId, List<long> TransactionIds) : ICommand<Unit>;

public sealed class AttachSupplierPaymentsCommandHandler(IApplicationDbContext db, IAuditService audit) : IRequestHandler<AttachSupplierPaymentsCommand, Unit>
{
    public async Task<Unit> Handle(AttachSupplierPaymentsCommand request, CancellationToken cancellationToken)
    {
        var ids = request.TransactionIds.Distinct().ToList();
        if (ids.Count == 0)
            return Unit.Value;

        var supply = await db.Supplies
            .Where(s => s.Id == request.SupplyId)
            .Select(s => new { s.Id, s.SupplierId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Ta'minot topilmadi.");

        if (supply.SupplierId is not { } supplierId)
            throw new BusinessRuleException("Ta'minotchisiz kirimga to'lov biriktirilmaydi.");

        var debtAccountIds = await db.Accounts
            .Where(a => a.SupplierId == supplierId && a.Type == AccountType.Debt)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        var transactions = await db.Transactions
            .Where(t => ids.Contains(t.Id)
                && t.OperationType == OperationType.DebtPay
                && t.SupplyId == null
                && t.FromAccountId != null
                && t.ToAccountId != null && debtAccountIds.Contains(t.ToAccountId.Value))
            .ToListAsync(cancellationToken);

        if (transactions.Count != ids.Count)
            throw new BusinessRuleException("To'lov bu ta'minotga biriktirilmaydi.");

        foreach (var transaction in transactions)
            transaction.SupplyId = request.SupplyId;

        audit.Add("payattach", "supplies", request.SupplyId, new { request.TransactionIds });
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed class AttachSupplierPaymentsCommandValidator : AbstractValidator<AttachSupplierPaymentsCommand>
{
    public AttachSupplierPaymentsCommandValidator()
    {
        RuleFor(x => x.TransactionIds).NotNull();
    }
}
