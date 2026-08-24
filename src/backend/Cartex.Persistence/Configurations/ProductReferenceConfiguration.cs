using Cartex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cartex.Persistence.Configurations;

public sealed class ProductReferenceConfiguration : IEntityTypeConfiguration<ProductReference>
{
    public void Configure(EntityTypeBuilder<ProductReference> builder)
    {
        builder.ToTable("product_reference");
        builder.Property(x => x.Barcode).HasMaxLength(60).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(300).IsRequired();
        builder.Property(x => x.UnitHint).HasMaxLength(100);
        builder.Property(x => x.CategoryHint).HasMaxLength(150);
        builder.Property(x => x.ManufacturerHint).HasMaxLength(150);
        builder.Property(x => x.PackQty).HasPrecision(12, 3);
        builder.Property(x => x.SuggestedPrice).HasPrecision(14, 2);
        builder.Property(x => x.SourceKey).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => x.Barcode).IsUnique();
        builder.HasIndex(x => x.Name, "ix_product_reference_name_trgm").HasDatabaseName("ix_product_reference_name_trgm").HasMethod("gin").HasOperators("gin_trgm_ops");
        builder.HasIndex(x => x.SearchFold, "ix_product_reference_search_fold_trgm").HasDatabaseName("ix_product_reference_search_fold_trgm").HasMethod("gin").HasOperators("gin_trgm_ops");
    }
}
