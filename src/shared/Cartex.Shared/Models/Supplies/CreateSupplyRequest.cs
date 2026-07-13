namespace Cartex.Shared.Models.Supplies;

// Miqdor va narx foydalanuvchi kiritgan ko'rinishda yuboriladi: birlik (UnitId) yoki qadoq (PackId)
// bo'yicha. PriceBasis narx nimaga tegishli ekanini aytadi: "PerEntry" (1 qop/1 t uchun) yoki
// "PerStockingUnit" (1 kg uchun). Saqlash birligiga o'girishni server bajaradi.
public record CreateSupplyItemRequest(
    long VariantId,
    decimal Quantity,
    decimal PurchasePrice,
    DateOnly? ExpiredAt,
    long? UnitId = null,
    decimal? SellingPrice = null,
    long? PackId = null,
    string PriceBasis = "PerEntry");

public record CreateSupplyRequest(
    long SupplierId,
    long WarehouseId,
    DateOnly SupplyDate,
    List<CreateSupplyItemRequest> Items,
    decimal PaidCash = 0,
    decimal PaidCard = 0,
    string? Currency = null);
