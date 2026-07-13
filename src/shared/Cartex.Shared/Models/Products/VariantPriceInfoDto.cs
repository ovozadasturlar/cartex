namespace Cartex.Shared.Models.Products;

// LastPurchasePrice saqlash birligida (mas. so'm/kg); LastEntry* esa oxirgi kirim aynan qanday
// kiritilgani — keyingi qatorni o'sha ko'rinishda ochish uchun.
public record VariantPriceInfoDto(
    decimal? LastPurchasePrice,
    decimal? SellingPrice,
    long? LastUnitId = null,
    decimal? LastPackSize = null,
    long? LastPackId = null,
    decimal? LastEntryPrice = null,
    string? LastPriceBasis = null);
