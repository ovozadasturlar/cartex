namespace Cartex.Mobile.Agent.Models;

public record SaleDraftItem(long VariantId, string Name, decimal Quantity, decimal UnitPrice);

public record SaleDraft(long WarehouseId, long? CustomerId, string? CustomerName, decimal PaidCash, List<SaleDraftItem> Items, DateOnly? DebtDueDate = null);

public record RepayDraft(long CustomerId, string CustomerName, decimal Amount);

public record OrderDraftItem(long VariantId, string Name, string UnitName, decimal Quantity, decimal UnitPrice);

public record OrderDraft(string LocalId, long WarehouseId, long? CustomerId, string? CustomerName, List<OrderDraftItem> Items);

public record CheckoutDraft(string Code, long? CustomerId, string CustomerName, decimal Total, decimal PaidCash, List<OrderDraftItem> Items);
