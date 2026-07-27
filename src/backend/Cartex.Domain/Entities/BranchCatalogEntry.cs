using Cartex.Domain.Common;
using Cartex.Domain.Enums;

namespace Cartex.Domain.Entities;

public class BranchCatalogEntry : SoftDeleteEntity, IBranchScoped
{
    public long BranchId { get; set; }
    public Branch Branch { get; set; } = null!;

    public long VariantId { get; set; }
    public ProductVariant Variant { get; set; } = null!;

    public DateTime? FirstActivityAt { get; set; }
    public DateTime? LastActivityAt { get; set; }
    public BranchCatalogActivationSource? ActivationSource { get; set; }
    public BranchCatalogVisibilityOverride VisibilityOverride { get; set; }
}
