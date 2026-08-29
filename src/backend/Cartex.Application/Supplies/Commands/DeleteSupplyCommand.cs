using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Application.Common.Shifts;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Application.Common.Finance;
using Cartex.Persistence;
using Cartex.Persistence.Services;
using Cartex.Domain.Enums;
using Microsoft.EntityFrameworkCore;

using Unit = Cartex.Application.Common.Messaging.Unit;

namespace Cartex.Application.Supplies.Commands;

public record DeleteSupplyCommand(long Id) : ICommand<Unit>;

public sealed class DeleteSupplyCommandHandler(IApplicationDbContext db, ICurrentUser currentUser, ILedgerService ledger, ISettingsService settingsService, IShiftLock shiftLock, IAuditService audit, InventoryReasonState inventoryReason) : IRequestHandler<DeleteSupplyCommand, Unit>
{
    public async Task<Unit> Handle(DeleteSupplyCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("Not authenticated.");

        var supply = await db.Supplies
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Ta'minot topilmadi.");

        var transactions = await db.Transactions
            .Where(t => t.SupplyId == supply.Id)
            .Include(t => t.FromAccount)
            .Include(t => t.ToAccount)
            .ToListAsync(cancellationToken);

        long? shiftId = null;
        if (transactions.Any(t => t.FromAccount?.Type == AccountType.Cash || t.ToAccount?.Type == AccountType.Cash))
        {
            shiftId = (await shiftLock.OpenAsync(userId, supply.BranchId, cancellationToken))?.Id;
            var policy = await settingsService.GetAsync<SalesPolicySettings>(SettingKeys.SalesPolicy, cancellationToken) ?? new SalesPolicySettings();
            if (shiftId is null && policy.ShiftPolicy != "Off")
                throw new BusinessRuleException("Naqd qaytarish uchun ochiq smena talab qilinadi.");
        }

        if ((await db.LockAsync<Supply>(
                $"SELECT * FROM supplies WHERE id = {supply.Id} AND is_deleted = false FOR UPDATE",
                cancellationToken)).Count == 0)
            throw new NotFoundException("Ta'minot topilmadi.");

        var stocks = await db.LockAsync<Stock>(
            $"SELECT * FROM stocks WHERE supply_id = {supply.Id} AND is_deleted = false ORDER BY id FOR UPDATE",
            cancellationToken);

        foreach (var group in supply.Items.GroupBy(i => i.VariantId))
        {
            if (stocks.Where(s => s.VariantId == group.Key).Sum(s => s.Quantity) != group.Sum(i => i.Quantity))
                throw new BusinessRuleException(
                    "Bu kirimdagi partiyalar o'zgargan (sotilgan, ishlatilgan yoki qaytarilgan) — bekor qilib bo'lmaydi.",
                    "supply_stock_modified");
        }

        await ledger.LockAsync(SupplyLedger.AccountIds(transactions), cancellationToken);

        foreach (var transaction in transactions)
            (await ledger.PostAsync(transaction.OperationType, transaction.Amount, transaction.ToAccount, transaction.FromAccount, userId, cancellationToken, shiftId, transaction.Rate)).Supply = supply;

        inventoryReason.Declare(new(InventoryMovementKind.SupplyReceipt, "Supply", supply.Id,
            InventoryLocation.External(supply.SupplierId ?? 0)));

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
