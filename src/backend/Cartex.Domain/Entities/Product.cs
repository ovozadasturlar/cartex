using Cartex.Domain.Common;

namespace Cartex.Domain.Entities;

public class Product : SoftDeleteEntity
{
    public long? CategoryId { get; set; }
    public Category? Category { get; set; }

    public long UnitId { get; set; }
    public Unit Unit { get; set; } = null!;

    public long? ProductTypeId { get; set; }
    public ProductType? ProductType { get; set; }

    public long? ManufacturerId { get; set; }
    public Manufacturer? Manufacturer { get; set; }

    public string Name { get; set; } = null!;
    public decimal MinStock { get; set; }
    public bool? TracksExpiryOverride { get; set; }
    public bool? AmountEntryEnabled { get; set; }
    public string? Attributes { get; set; }
    public string? IkpuCode { get; set; }
    public decimal? VatRate { get; set; }
    public string? ImageKey { get; set; }
    public bool IsEnabled { get; set; } = true;

    public ICollection<ProductVariant> Variants { get; set; } = [];
    public ICollection<ProductPack> Packs { get; set; } = [];
}
