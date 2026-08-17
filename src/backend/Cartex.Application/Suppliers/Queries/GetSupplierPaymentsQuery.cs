using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Cartex.Shared.Models.Suppliers;

namespace Cartex.Application.Suppliers.Queries;

public record GetSupplierPaymentsQuery(long SupplierId, DateOnly Date) : IRequest<IReadOnlyCollection<SupplierPaymentDto>>;

public sealed class GetSupplierPaymentsQueryHandler(IApplicationDbContext db) : IRequestHandler<GetSupplierPaymentsQuery, IReadOnlyCollection<SupplierPaymentDto>>
{
    public async Task<IReadOnlyCollection<SupplierPaymentDto>> Handle(GetSupplierPaymentsQuery request, CancellationToken cancellationToken)
    {
        var debtAccountIds = await db.Accounts
            .Where(a => a.SupplierId == request.SupplierId && a.Type == AccountType.Debt)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        if (debtAccountIds.Count == 0)
            return [];

        var from = request.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local).ToUniversalTime();
        var to = from.AddDays(1);

        return await db.Transactions
            .Where(t => t.ToAccountId != null && debtAccountIds.Contains(t.ToAccountId.Value)
                && t.FromAccount != null
                && t.OperationType == OperationType.DebtPay
                && t.SupplyId == null
                && t.CreatedAt >= from && t.CreatedAt < to)
            .OrderBy(t => t.CreatedAt).ThenBy(t => t.Id)
            .Select(t => new SupplierPaymentDto(
                t.Id,
                t.CreatedAt,
                t.Amount,
                t.Currency,
                t.FromAccount!.Type.ToString(),
                t.User.FullName))
            .ToListAsync(cancellationToken);
    }
}
