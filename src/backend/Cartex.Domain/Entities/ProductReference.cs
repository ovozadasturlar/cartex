using Cartex.Domain.Common;

namespace Cartex.Domain.Entities;

public sealed class ProductReference : AuditableEntity
{
    public string Barcode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? SearchFold { get; set; }
    public string? UnitHint { get; set; }
    public string? CategoryHint { get; set; }
    public string? ManufacturerHint { get; set; }
    public decimal? PackQty { get; set; }
    public decimal? SuggestedPrice { get; set; }
    public string SourceKey { get; set; } = string.Empty;
    public DateTime SyncedAt { get; set; }
}
