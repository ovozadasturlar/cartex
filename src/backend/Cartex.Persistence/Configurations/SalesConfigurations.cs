using Cartex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cartex.Persistence.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");
        builder.Property(x => x.FullName).IsRequired();
        builder.Property(x => x.Phone).HasMaxLength(20);
        builder.HasIndex(x => x.Phone).IsUnique().HasFilter("\"phone\" IS NOT NULL");
        builder.Property(x => x.CardBarcode).HasMaxLength(60);
        builder.HasIndex(x => x.CardBarcode).IsUnique().HasFilter("\"card_barcode\" IS NOT NULL");
        builder.Property(x => x.DiscountPct).HasPrecision(5, 2);
    }
}

public class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> builder)
    {
        builder.ToTable("sales");
        builder.Property(x => x.TotalAmount).HasPrecision(18, 2);
        builder.Property(x => x.PaidCash).HasPrecision(18, 2);
        builder.Property(x => x.PaidCard).HasPrecision(18, 2);
        builder.Property(x => x.PaidBonus).HasPrecision(18, 2);
        builder.Property(x => x.DebtAmount).HasPrecision(18, 2);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(15);
        builder.Property(x => x.ReceiptToken).HasMaxLength(40).IsRequired();
        builder.HasIndex(x => x.ReceiptToken).IsUnique();
        builder.HasIndex(x => x.BranchId);

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Warehouse)
            .WithMany()
            .HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Customer)
            .WithMany(c => c.Sales)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class SaleItemConfiguration : IEntityTypeConfiguration<SaleItem>
{
    public void Configure(EntityTypeBuilder<SaleItem> builder)
    {
        builder.ToTable("sale_items");
        builder.Property(x => x.Quantity).HasPrecision(12, 3);
        builder.Property(x => x.UnitPrice).HasPrecision(14, 2);
        builder.Property(x => x.PurchasePrice).HasPrecision(14, 2);

        builder.HasOne(x => x.Sale)
            .WithMany(s => s.Items)
            .HasForeignKey(x => x.SaleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
