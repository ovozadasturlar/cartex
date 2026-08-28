using Cartex.Application.Common.Finance;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Persistence.Services;

namespace Cartex.Application.StockWriteOffs;

internal static class WriteOffDocuments
{
    public static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static decimal ClaimAmount(IEnumerable<StockWriteOffLine> lines) =>
        lines.Where(x => x.Disposition == InventoryDisposition.SupplierClaim).Sum(x => x.LineCost);

    public static async Task MoveStockAsync(
        IApplicationDbContext db,
        InventoryReasonState inventoryReason,
        StockWriteOffDocument document,
        CancellationToken cancellationToken)
    {
        foreach (var group in document.Lines.GroupBy(x => x.Disposition).OrderBy(x => x.Key))
        {
            inventoryReason.Declare(new InventoryReason(InventoryMovementKind.WriteOff, "StockWriteOff", document.Id,
                InventoryLocation.Of(group.Key, document.WarehouseId)));
            foreach (var line in group)
                line.Stock.Quantity -= line.Quantity;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public static async Task PostSupplierClaimsAsync(
        ILedgerService ledger,
        StockWriteOffDocument document,
        long userId,
        CancellationToken cancellationToken)
    {
        foreach (var group in document.Lines
                     .Where(x => x.Disposition == InventoryDisposition.SupplierClaim && x.SupplierId is not null)
                     .GroupBy(x => new { SupplierId = x.SupplierId!.Value, x.ClaimCurrency, x.ClaimRate }))
        {
            var rate = group.Key.ClaimRate == 0 ? 1m : group.Key.ClaimRate;
            var amount = Math.Round(group.Sum(x => x.LineCost) / rate, 4);
            if (amount == 0)
                continue;

            var account = await ledger.SupplierAccountAsync(
                group.Key.SupplierId, AccountType.Debt, cancellationToken, group.Key.ClaimCurrency);
            var transaction = amount > 0
                ? await ledger.PostAsync(OperationType.SupplierClaim, amount, null, account, userId, cancellationToken, rate: rate)
                : await ledger.PostAsync(OperationType.SupplierClaim, -amount, account, null, userId, cancellationToken, rate: rate);
            transaction.StockWriteOffDocument = document;
            transaction.Description = document.DocumentNumber;
        }
    }
}
