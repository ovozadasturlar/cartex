namespace Cartex.Mobile.Agent.Models;

public record SaleDraftItem(long VariantId, string Name, decimal Quantity, decimal UnitPrice);

public record SaleDraft(long WarehouseId, long? CustomerId, string? CustomerName, decimal PaidCash, List<SaleDraftItem> Items, DateOnly? DebtDueDate = null);

public record RepayDraft(long CustomerId, string CustomerName, decimal Amount);
