using Cartex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cartex.Persistence.Configurations;

public class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> builder)
    {
        builder.ToTable("warehouses");
        builder.Property(x => x.Name).IsRequired();
        builder.HasIndex(x => x.BranchId);

        builder.HasOne(x => x.Branch)
            .WithMany(s => s.Warehouses)
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.AssignedUser)
            .WithMany()
            .HasForeignKey(x => x.AssignedUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class PrepackConfiguration : IEntityTypeConfiguration<Prepack>
{
    public void Configure(EntityTypeBuilder<Prepack> builder)
    {
        builder.ToTable("prepacks");
        builder.Property(x => x.Quantity).HasPrecision(12, 3);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 6);
        builder.Property(x => x.LabelCode).HasMaxLength(30).IsRequired();
        builder.HasIndex(x => x.LabelCode).IsUnique().HasFilter("NOT \"is_deleted\"");
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(15);
        builder.HasIndex(x => new { x.WarehouseId, x.Status });

        builder.HasOne(x => x.Warehouse)
            .WithMany()
            .HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Variant)
            .WithMany()
            .HasForeignKey(x => x.VariantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class StockConfiguration : IEntityTypeConfiguration<Stock>
{
    public void Configure(EntityTypeBuilder<Stock> builder)
    {
        builder.ToTable("stocks");
        builder.Property(x => x.Quantity).HasPrecision(12, 3);
        builder.Property(x => x.PurchasePrice).HasPrecision(18, 4);
        builder.HasIndex(x => x.BranchId);
        builder.HasIndex(x => new { x.WarehouseId, x.VariantId });
        builder.HasIndex(x => new { x.WarehouseId, x.VariantId }).IsUnique().HasFilter("\"is_deficit\" AND NOT \"is_deleted\"");

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Variant)
            .WithMany(v => v.Stocks)
            .HasForeignKey(x => x.VariantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Warehouse)
            .WithMany(w => w.Stocks)
            .HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Supply)
            .WithMany()
            .HasForeignKey(x => x.SupplyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class StockAdjustmentConfiguration : IEntityTypeConfiguration<StockAdjustment>
{
    public void Configure(EntityTypeBuilder<StockAdjustment> builder)
    {
        builder.ToTable("stock_adjustments");
        builder.Property(x => x.SystemQuantity).HasPrecision(12, 3);
        builder.Property(x => x.CountedQuantity).HasPrecision(12, 3);
        builder.Property(x => x.Difference).HasPrecision(12, 3);
        builder.Property(x => x.Reason).HasMaxLength(250);
        builder.HasIndex(x => x.BranchId);

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Warehouse>()
            .WithMany()
            .HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ProductVariant>()
            .WithMany()
            .HasForeignKey(x => x.VariantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ProductPriceConfiguration : IEntityTypeConfiguration<ProductPrice>
{
    public void Configure(EntityTypeBuilder<ProductPrice> builder)
    {
        builder.ToTable("product_prices");
        builder.Property(x => x.Currency).HasMaxLength(3);
        builder.Property(x => x.SellingPrice).HasPrecision(14, 2);

        builder.HasOne(x => x.Variant)
            .WithMany(v => v.Prices)
            .HasForeignKey(x => x.VariantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Warehouse)
            .WithMany()
            .HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.VariantId)
            .IsUnique()
            .HasFilter("\"warehouse_id\" IS NULL");

        builder.HasIndex(x => new { x.VariantId, x.WarehouseId })
            .IsUnique()
            .HasFilter("\"warehouse_id\" IS NOT NULL");
    }
}

public class StockTransferConfiguration : IEntityTypeConfiguration<StockTransfer>
{
    public void Configure(EntityTypeBuilder<StockTransfer> builder)
    {
        builder.ToTable("stock_transfers");
        builder.Property(x => x.Quantity).HasPrecision(12, 3);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(x => x.BranchId);
        builder.HasIndex(x => x.CreatedAt);

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.FromWarehouse)
            .WithMany()
            .HasForeignKey(x => x.FromWarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ToWarehouse)
            .WithMany()
            .HasForeignKey(x => x.ToWarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Variant)
            .WithMany()
            .HasForeignKey(x => x.VariantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ProductPriceHistoryConfiguration : IEntityTypeConfiguration<ProductPriceHistory>
{
    public void Configure(EntityTypeBuilder<ProductPriceHistory> builder)
    {
        builder.ToTable("product_price_history");
        builder.Property(x => x.Currency).HasMaxLength(3);
        builder.Property(x => x.SellingPrice).HasPrecision(14, 2);

        builder.HasOne(x => x.Variant)
            .WithMany()
            .HasForeignKey(x => x.VariantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Warehouse)
            .WithMany()
            .HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Cascade);

        // NARX-10: qidiruv har doim "shu variant, shu ombor, oynadan keyin tugagan" kesimida boradi.
        builder.HasIndex(x => new { x.VariantId, x.WarehouseId, x.EffectiveTo });
    }
}

public class StockWriteOffDocumentConfiguration : IEntityTypeConfiguration<StockWriteOffDocument>
{
    public void Configure(EntityTypeBuilder<StockWriteOffDocument> builder)
    {
        builder.ToTable("stock_write_off_documents");
        builder.Property(x => x.DocumentNumber).HasMaxLength(40).IsRequired();
        builder.Property(x => x.TotalCost).HasPrecision(18, 2);
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(128);
        builder.HasIndex(x => x.DocumentNumber).IsUnique();
        builder.HasIndex(x => new { x.BranchId, x.IdempotencyKey }).IsUnique()
            .HasFilter("\"idempotency_key\" IS NOT NULL");
        builder.HasIndex(x => new { x.BranchId, x.BusinessDate, x.Id });
        builder.HasIndex(x => x.ReversesDocumentId).IsUnique()
            .HasFilter("\"reverses_document_id\" IS NOT NULL");

        builder.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ReversesDocument).WithMany().HasForeignKey(x => x.ReversesDocumentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class StockWriteOffLineConfiguration : IEntityTypeConfiguration<StockWriteOffLine>
{
    public void Configure(EntityTypeBuilder<StockWriteOffLine> builder)
    {
        builder.ToTable("stock_write_off_lines");
        builder.Property(x => x.Quantity).HasPrecision(12, 3);
        builder.Property(x => x.UnitCost).HasPrecision(18, 4);
        builder.Property(x => x.LineCost).HasPrecision(18, 2);
        builder.Property(x => x.ClaimCurrency).HasMaxLength(3);
        builder.Property(x => x.ClaimRate).HasPrecision(18, 6);
        builder.Property(x => x.Reason).HasConversion<string>().HasMaxLength(15);
        builder.Property(x => x.Disposition).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.HasIndex(x => new { x.VariantId, x.Reason });

        builder.HasOne(x => x.Document).WithMany(x => x.Lines)
            .HasForeignKey(x => x.StockWriteOffDocumentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Variant).WithMany().HasForeignKey(x => x.VariantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Stock).WithMany().HasForeignKey(x => x.StockId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
    }
}
