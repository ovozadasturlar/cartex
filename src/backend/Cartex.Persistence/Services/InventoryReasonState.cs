using Cartex.Domain.Enums;

namespace Cartex.Persistence.Services;

public readonly record struct InventoryLocation(InventoryLocationKind Kind, long Id)
{
    public static InventoryLocation External(long id = 0) => new(InventoryLocationKind.External, id);
    public static InventoryLocation Warehouse(long id) => new(InventoryLocationKind.Warehouse, id);
    public static InventoryLocation Customer(long id) => new(InventoryLocationKind.Customer, id);

    public static InventoryLocation Of(InventoryDisposition disposition, long warehouseId) => new(disposition switch
    {
        InventoryDisposition.Quarantine => InventoryLocationKind.Quarantine,
        InventoryDisposition.Scrap => InventoryLocationKind.Scrap,
        InventoryDisposition.SupplierClaim => InventoryLocationKind.SupplierClaim,
        _ => InventoryLocationKind.Warehouse
    }, warehouseId);
}

public sealed record InventoryReason(
    InventoryMovementKind Kind,
    string SourceType,
    long? SourceId,
    InventoryLocation Inbound,
    InventoryLocation Outbound)
{
    public InventoryReason(InventoryMovementKind kind, string sourceType, long? sourceId, InventoryLocation counterparty)
        : this(kind, sourceType, sourceId, counterparty, counterparty) { }
}

public sealed class InventoryReasonState
{
    public InventoryReason? Current { get; private set; }

    public void Declare(InventoryReason reason) => Current = reason;

    public void Release() => Current = null;
}
