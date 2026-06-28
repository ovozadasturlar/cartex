using Cartex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cartex.Persistence.Configurations;

public class ShopConfiguration : IEntityTypeConfiguration<Shop>
{
    public void Configure(EntityTypeBuilder<Shop> builder)
    {
        builder.ToTable("shops");
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Phone).HasMaxLength(20);
        builder.Property(x => x.CashbackRate).HasPrecision(5, 2);
    }
}

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");
        builder.Property(x => x.Name).HasMaxLength(30).IsRequired();
        builder.HasIndex(x => x.Name).IsUnique();
    }
}

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("permissions");
        builder.Property(x => x.Name).HasMaxLength(60).IsRequired();
        builder.HasIndex(x => x.Name).IsUnique();
    }
}

public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("role_permissions");
        builder.HasKey(x => new { x.RoleId, x.PermissionId });

        builder.HasOne(x => x.Role)
            .WithMany(r => r.RolePermissions)
            .HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Permission)
            .WithMany(p => p.RolePermissions)
            .HasForeignKey(x => x.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.Property(x => x.FullName).IsRequired();
        builder.Property(x => x.Username).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.Username).IsUnique();

        builder.HasOne(x => x.Shop)
            .WithMany(s => s.Users)
            .HasForeignKey(x => x.ShopId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Role)
            .WithMany(r => r.Users)
            .HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

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
    }
}

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");
        builder.Property(x => x.Name).IsRequired();
        builder.Property(x => x.MinStock).HasPrecision(12, 3);

        builder.HasOne(x => x.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Unit)
            .WithMany(u => u.Products)
            .HasForeignKey(x => x.UnitId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class BarcodeConfiguration : IEntityTypeConfiguration<Barcode>
{
    public void Configure(EntityTypeBuilder<Barcode> builder)
    {
        builder.ToTable("barcodes");
        builder.Property(x => x.Code).HasMaxLength(60).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.PackQty).HasPrecision(12, 3);

        builder.HasOne(x => x.Product)
            .WithMany(p => p.Barcodes)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> builder)
    {
        builder.ToTable("warehouses");
        builder.Property(x => x.Name).IsRequired();

        builder.HasOne(x => x.Shop)
            .WithMany(s => s.Warehouses)
            .HasForeignKey(x => x.ShopId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class StockConfiguration : IEntityTypeConfiguration<Stock>
{
    public void Configure(EntityTypeBuilder<Stock> builder)
    {
        builder.ToTable("stocks");
        builder.Property(x => x.Quantity).HasPrecision(12, 3);
        builder.Property(x => x.PurchasePrice).HasPrecision(14, 2);
        builder.Property(x => x.SellingPrice).HasPrecision(14, 2);

        builder.HasOne(x => x.Product)
            .WithMany(p => p.Stocks)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Warehouse)
            .WithMany(w => w.Stocks)
            .HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class StockTransferConfiguration : IEntityTypeConfiguration<StockTransfer>
{
    public void Configure(EntityTypeBuilder<StockTransfer> builder)
    {
        builder.ToTable("stock_transfers");
        builder.Property(x => x.Quantity).HasPrecision(12, 3);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(x => x.FromWarehouse)
            .WithMany()
            .HasForeignKey(x => x.FromWarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ToWarehouse)
            .WithMany()
            .HasForeignKey(x => x.ToWarehouseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

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
        builder.Property(x => x.CashbackBalance).HasPrecision(18, 2);
    }
}

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("suppliers");
        builder.Property(x => x.Name).IsRequired();
        builder.Property(x => x.Phone).HasMaxLength(20);
    }
}

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts");
        builder.Property(x => x.OwnerType).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Name).HasMaxLength(60);
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Balance).HasPrecision(18, 2);
    }
}

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("transactions");
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.OperationType).HasConversion<string>().HasMaxLength(30);

        builder.HasOne(x => x.FromAccount)
            .WithMany()
            .HasForeignKey(x => x.FromAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ToAccount)
            .WithMany()
            .HasForeignKey(x => x.ToAccountId)
            .OnDelete(DeleteBehavior.Restrict);
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

public class SupplyConfiguration : IEntityTypeConfiguration<Supply>
{
    public void Configure(EntityTypeBuilder<Supply> builder)
    {
        builder.ToTable("supplies");
        builder.Property(x => x.TotalAmount).HasPrecision(18, 2);

        builder.HasOne(x => x.Supplier)
            .WithMany(s => s.Supplies)
            .HasForeignKey(x => x.SupplierId)
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
        builder.Property(x => x.PurchasePrice).HasPrecision(14, 2);

        builder.HasOne(x => x.Supply)
            .WithMany(s => s.Items)
            .HasForeignKey(x => x.SupplyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");
        builder.Property(x => x.Action).HasMaxLength(15);
        builder.Property(x => x.TableName).HasMaxLength(40);
        builder.Property(x => x.OldData).HasColumnType("jsonb");
        builder.Property(x => x.NewData).HasColumnType("jsonb");
    }
}
