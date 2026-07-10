using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Domain.Common;
using Cartex.Application.Common.Finance;
using Cartex.Persistence;
using Cartex.Domain.Enums;
using Microsoft.EntityFrameworkCore;

using Unit = Cartex.Application.Common.Messaging.Unit;

namespace Cartex.Application.Supplies.Commands;

public record DeleteSupplyCommand(long Id) : ICommand<Unit>;

public sealed class DeleteSupplyCommandHandler(IApplicationDbContext db, ICurrentUser currentUser, ILedgerService ledger, ISettingsService settingsService, IAuditService audit) : IRequestHandler<DeleteSupplyCommand, Unit>
{
    public async Task<Unit> Handle(DeleteSupplyCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("Not authenticated.");

        var supply = await db.Supplies
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Ta'minot topilmadi.");

        var stocks = await db.Stocks.Where(s => s.SupplyId == supply.Id).ToListAsync(cancellationToken);

        foreach (var group in supply.Items.GroupBy(i => i.VariantId))
        {
            var remaining = stocks.Where(s => s.VariantId == group.Key).Sum(s => s.Quantity);
            if (remaining < group.Sum(i => i.Quantity))
                throw new BusinessRuleException("Bu kirimdagi mahsulotlardan sotilgan yoki ishlatilgan — bekor qilib bo'lmaydi.");
        }

        var transactions = await db.Transactions
            .Where(t => t.SupplyId == supply.Id)
            .Include(t => t.FromAccount)
            .Include(t => t.ToAccount)
            .ToListAsync(cancellationToken);

        long? shiftId = null;
        if (transactions.Any(t => t.FromAccount?.Type == AccountType.Cash || t.ToAccount?.Type == AccountType.Cash))
        {
            shiftId = await db.Shifts
                .Where(s => s.UserId == userId && s.BranchId == supply.BranchId && s.Status == ShiftStatus.Open)
                .Select(s => (long?)s.Id)
                .FirstOrDefaultAsync(cancellationToken);
            var policy = await settingsService.GetAsync<SalesPolicySettings>(SettingKeys.SalesPolicy, cancellationToken) ?? new SalesPolicySettings();
            if (shiftId is null && policy.ShiftPolicy != "Off")
                throw new BusinessRuleException("Naqd qaytarish uchun ochiq smena talab qilinadi.");
        }

        foreach (var transaction in transactions)
            ledger.Post(transaction.OperationType, transaction.Amount, transaction.ToAccount, transaction.FromAccount, userId, shiftId, transaction.Rate).Supply = supply;

        foreach (var stock in stocks)
        {
            stock.IsDeleted = true;
            stock.DeletedAt = DateTime.UtcNow;
        }

        supply.IsDeleted = true;
        supply.DeletedAt = DateTime.UtcNow;

        audit.Add("supply.delete", "supplies", supply.Id, new { supply.SupplierId, supply.TotalAmount });
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
