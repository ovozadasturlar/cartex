using Cartex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cartex.Persistence.Configurations;

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("suppliers");
        builder.Property(x => x.Name).IsRequired();
        builder.Property(x => x.Phone).HasMaxLength(20);
    }
}

public class SupplyConfiguration : IEntityTypeConfiguration<Supply>
{
    public void Configure(EntityTypeBuilder<Supply> builder)
    {
        builder.ToTable("supplies");
        builder.Property(x => x.Currency).HasMaxLength(3);
        builder.Property(x => x.Rate).HasPrecision(18, 6);
        builder.Property(x => x.TotalAmount).HasPrecision(18, 2);
        builder.HasIndex(x => x.BranchId);
        builder.HasIndex(x => x.CreatedAt);

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Supplier)
            .WithMany(s => s.Supplies)
            .HasForeignKey(x => x.SupplierId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Warehouse)
            .WithMany()
            .HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class SupplyItemConfiguration : IEntityTypeConfiguration<SupplyItem>
{
    public void Configure(EntityTypeBuilder<SupplyItem> builder)
    {
        builder.ToTable("supply_items");
        builder.Property(x => x.Quantity).HasPrecision(12, 3);
        builder.Property(x => x.PackSize).HasPrecision(12, 3).HasDefaultValue(1m);
        // Narx saqlash birligiga o'girilgani uchun juda kichik bo'lishi mumkin (mas. tonna narxi -> so'm/g).
        builder.Property(x => x.PurchasePrice).HasPrecision(18, 4);
        builder.Property(x => x.EntryQuantity).HasPrecision(12, 3);
        builder.Property(x => x.EntryPrice).HasPrecision(18, 4);
        builder.Property(x => x.PriceBasis).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(x => x.Pack)
            .WithMany()
            .HasForeignKey(x => x.PackId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Unit)
            .WithMany()
            .HasForeignKey(x => x.UnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Supply)
            .WithMany(s => s.Items)
            .HasForeignKey(x => x.SupplyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Variant)
            .WithMany()
            .HasForeignKey(x => x.VariantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
