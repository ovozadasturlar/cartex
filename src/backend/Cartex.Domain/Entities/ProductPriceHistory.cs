namespace Cartex.Domain.Entities;

/// NARX-14: katalog narxining tugagan versiyasi. Har yozuv narxning qachondan qachongacha
/// amal qilganini saqlaydi, shunda savdo yakunlanayotganda "kassir ko'rgan son yaqinda
/// haqiqatan katalogda turganmi" degan savolga taxminsiz javob beriladi.
public class ProductPriceHistory
{
    public long Id { get; set; }

    public long VariantId { get; set; }
    public ProductVariant Variant { get; set; } = null!;

    public long? WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    public decimal SellingPrice { get; set; }
    public string Currency { get; set; } = "UZS";

    public DateTime EffectiveFrom { get; set; }
    public DateTime EffectiveTo { get; set; }
}
