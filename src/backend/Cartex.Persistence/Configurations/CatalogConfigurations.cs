using Cartex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cartex.Persistence.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");
        builder.Property(x => x.Name).IsRequired();

        builder.HasOne(x => x.Parent)
            .WithMany(c => c.Children)
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class UnitConfiguration : IEntityTypeConfiguration<Unit>
{
    public void Configure(EntityTypeBuilder<Unit> builder)
    {
        builder.ToTable("units");
        builder.Property(x => x.Name).HasMaxLength(20).IsRequired();
        builder.Property(x => x.ShortName).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Dimension).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Factor).HasPrecision(18, 6);
        builder.Property(x => x.IsEnabled).HasDefaultValue(true);
    }
}

public class ProductTypeConfiguration : IEntityTypeConfiguration<ProductType>
{
    public void Configure(EntityTypeBuilder<ProductType> builder)
    {
        builder.ToTable("product_types");
        builder.Property(x => x.Name).HasMaxLength(50).IsRequired();
        builder.Property(x => x.AttributeSchema).HasColumnType("jsonb");
    }
}

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");
        builder.Property(x => x.Name).IsRequired();
        builder.Property(x => x.MinStock).HasPrecision(12, 3);
        builder.Property(x => x.Attributes).HasColumnType("jsonb");
        builder.Property(x => x.IkpuCode).HasMaxLength(30);
        builder.Property(x => x.VatRate).HasPrecision(5, 2);
        builder.Property(x => x.ImageKey).HasMaxLength(200);

        builder.HasOne(x => x.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Unit)
            .WithMany(u => u.Products)
            .HasForeignKey(x => x.UnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProductType)
            .WithMany(t => t.Products)
            .HasForeignKey(x => x.ProductTypeId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Manufacturer)
            .WithMany()
            .HasForeignKey(x => x.ManufacturerId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.ToTable("product_variants");
        builder.Property(x => x.Name).HasMaxLength(100);
        builder.Property(x => x.Code).HasMaxLength(60);
        builder.Property(x => x.Attributes).HasColumnType("jsonb");
        builder.Property(x => x.ImageKey).HasMaxLength(200);
        builder.HasIndex(x => x.ProductId);
        builder.HasIndex(x => x.Code)
            .IsUnique()
            .HasFilter("\"code\" IS NOT NULL AND \"is_deleted\" = false");

        builder.HasOne(x => x.Product)
            .WithMany(p => p.Variants)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class BarcodeConfiguration : IEntityTypeConfiguration<Barcode>
{
    public void Configure(EntityTypeBuilder<Barcode> builder)
    {
        builder.ToTable("barcodes");
        builder.Property(x => x.Code).HasMaxLength(60).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique().HasFilter("\"is_deleted\" = false");
        builder.Property(x => x.PackQty).HasPrecision(12, 3);

        builder.HasOne(x => x.Variant)
            .WithMany(v => v.Barcodes)
            .HasForeignKey(x => x.VariantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
