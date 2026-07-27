using Cartex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cartex.Persistence.Configurations;

public class BranchCatalogEntryConfiguration : IEntityTypeConfiguration<BranchCatalogEntry>
{
    public void Configure(EntityTypeBuilder<BranchCatalogEntry> builder)
    {
        builder.ToTable("branch_catalog_entries");
        builder.Property(x => x.VisibilityOverride).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ActivationSource).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(x => new { x.BranchId, x.VariantId }).IsUnique().HasFilter("NOT \"is_deleted\"");
        builder.HasIndex(x => new { x.BranchId, x.VisibilityOverride });

        builder.HasOne(x => x.Branch)
            .WithMany(x => x.CatalogEntries)
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Variant)
            .WithMany()
            .HasForeignKey(x => x.VariantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
