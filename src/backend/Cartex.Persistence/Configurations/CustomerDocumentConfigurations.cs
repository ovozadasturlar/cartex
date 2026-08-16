using Cartex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cartex.Persistence.Configurations;

public class CustomerPaymentDocumentConfiguration : IEntityTypeConfiguration<CustomerPaymentDocument>
{
    public void Configure(EntityTypeBuilder<CustomerPaymentDocument> builder)
    {
        builder.ToTable("customer_payment_documents");
        builder.Property(x => x.DocumentNumber).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(15);
        builder.Property(x => x.TotalBaseAmount).HasPrecision(18, 2);
        builder.Property(x => x.AllocatedBaseAmount).HasPrecision(18, 2);
        builder.Property(x => x.AdvanceBaseAmount).HasPrecision(18, 2);
        builder.Property(x => x.WriteOffBaseAmount).HasPrecision(18, 2);
        builder.Property(x => x.WriteOffReason).HasMaxLength(500);
        builder.Property(x => x.BalanceAfterBase).HasPrecision(18, 2);
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(128);
        builder.HasIndex(x => x.DocumentNumber).IsUnique();
        builder.HasIndex(x => new { x.BranchId, x.IdempotencyKey }).IsUnique()
            .HasFilter("\"idempotency_key\" IS NOT NULL");
        builder.HasIndex(x => new { x.CustomerId, x.BusinessDate, x.Id });

        builder.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class CustomerPaymentTenderConfiguration : IEntityTypeConfiguration<CustomerPaymentTender>
{
    public void Configure(EntityTypeBuilder<CustomerPaymentTender> builder)
    {
        builder.ToTable("customer_payment_tenders");
        builder.Property(x => x.Method).HasConversion<string>().HasMaxLength(15);
        builder.Property(x => x.Currency).HasMaxLength(3);
        builder.Property(x => x.Amount).HasPrecision(18, 4);
        builder.Property(x => x.Rate).HasPrecision(18, 6);
        builder.Property(x => x.AmountBase).HasPrecision(18, 2);
        builder.HasOne(x => x.Document).WithMany(x => x.Tenders)
            .HasForeignKey(x => x.CustomerPaymentDocumentId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class CustomerPaymentAllocationConfiguration : IEntityTypeConfiguration<CustomerPaymentAllocation>
{
    public void Configure(EntityTypeBuilder<CustomerPaymentAllocation> builder)
    {
        builder.ToTable("customer_payment_allocations");
        builder.Property(x => x.Currency).HasMaxLength(3);
        builder.Property(x => x.Amount).HasPrecision(18, 4);
        builder.Property(x => x.Rate).HasPrecision(18, 6);
        builder.Property(x => x.AmountBase).HasPrecision(18, 2);
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(10);
        builder.HasOne(x => x.Document).WithMany(x => x.Allocations)
            .HasForeignKey(x => x.CustomerPaymentDocumentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Sale).WithMany().HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.SaleId);
    }
}

public class CustomerRefundDocumentConfiguration : IEntityTypeConfiguration<CustomerRefundDocument>
{
    public void Configure(EntityTypeBuilder<CustomerRefundDocument> builder)
    {
        builder.ToTable("customer_refund_documents");
        builder.Property(x => x.DocumentNumber).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(15);
        builder.Property(x => x.TotalBaseAmount).HasPrecision(18, 2);
        builder.Property(x => x.AdvanceBaseAmount).HasPrecision(18, 2);
        builder.Property(x => x.LoanBaseAmount).HasPrecision(18, 2);
        builder.Property(x => x.BalanceAfterBase).HasPrecision(18, 2);
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(128);
        builder.HasIndex(x => x.DocumentNumber).IsUnique();
        builder.HasIndex(x => new { x.BranchId, x.IdempotencyKey }).IsUnique()
            .HasFilter("\"idempotency_key\" IS NOT NULL");
        builder.HasIndex(x => new { x.CustomerId, x.BusinessDate, x.Id });

        builder.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class CustomerRefundTenderConfiguration : IEntityTypeConfiguration<CustomerRefundTender>
{
    public void Configure(EntityTypeBuilder<CustomerRefundTender> builder)
    {
        builder.ToTable("customer_refund_tenders");
        builder.Property(x => x.Method).HasConversion<string>().HasMaxLength(15);
        builder.Property(x => x.Currency).HasMaxLength(3);
        builder.Property(x => x.Amount).HasPrecision(18, 4);
        builder.Property(x => x.Rate).HasPrecision(18, 6);
        builder.Property(x => x.AmountBase).HasPrecision(18, 2);
        builder.HasOne(x => x.Document).WithMany(x => x.Tenders)
            .HasForeignKey(x => x.CustomerRefundDocumentId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class CustomerReturnDocumentConfiguration : IEntityTypeConfiguration<CustomerReturnDocument>
{
    public void Configure(EntityTypeBuilder<CustomerReturnDocument> builder)
    {
        builder.ToTable("customer_return_documents");
        builder.Property(x => x.DocumentNumber).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(15);
        builder.Property(x => x.GrossAmount).HasPrecision(18, 2);
        builder.Property(x => x.RefundAmount).HasPrecision(18, 2);
        builder.Property(x => x.CashbackReversed).HasPrecision(18, 2);
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(128);
        builder.HasIndex(x => x.DocumentNumber).IsUnique();
        builder.HasIndex(x => new { x.BranchId, x.IdempotencyKey }).IsUnique()
            .HasFilter("\"idempotency_key\" IS NOT NULL");
        builder.HasIndex(x => new { x.CustomerId, x.BusinessDate, x.Id });
        builder.HasIndex(x => new { x.BranchId, x.BusinessDate, x.Id });

        builder.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class CustomerReturnLineConfiguration : IEntityTypeConfiguration<CustomerReturnLine>
{
    public void Configure(EntityTypeBuilder<CustomerReturnLine> builder)
    {
        builder.ToTable("customer_return_lines");
        builder.Property(x => x.Quantity).HasPrecision(12, 3);
        builder.Property(x => x.UnitPrice).HasPrecision(14, 2);
        builder.Property(x => x.PriceCurrency).HasMaxLength(3);
        builder.Property(x => x.PriceRate).HasPrecision(18, 6);
        builder.Property(x => x.LineAmount).HasPrecision(18, 2);
        builder.Property(x => x.CashbackReversed).HasPrecision(18, 2);
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.Property(x => x.Condition).HasConversion<string>().HasMaxLength(15);
        builder.Property(x => x.Disposition).HasConversion<string>().HasMaxLength(20);
        builder.HasOne(x => x.Document).WithMany(x => x.Lines)
            .HasForeignKey(x => x.CustomerReturnDocumentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Sale).WithMany().HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SaleItem).WithMany().HasForeignKey(x => x.SaleItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Stock).WithMany().HasForeignKey(x => x.StockId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Variant).WithMany().HasForeignKey(x => x.VariantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.SaleItemId);
        builder.HasIndex(x => x.SaleId);
    }
}

public class CustomerReturnSettlementConfiguration : IEntityTypeConfiguration<CustomerReturnSettlement>
{
    public void Configure(EntityTypeBuilder<CustomerReturnSettlement> builder)
    {
        builder.ToTable("customer_return_settlements");
        builder.Property(x => x.Method).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Currency).HasMaxLength(3);
        builder.Property(x => x.Amount).HasPrecision(18, 4);
        builder.Property(x => x.Rate).HasPrecision(18, 6);
        builder.Property(x => x.AmountBase).HasPrecision(18, 2);
        builder.HasOne(x => x.Document).WithMany(x => x.Settlements)
            .HasForeignKey(x => x.CustomerReturnDocumentId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class InventoryPositionConfiguration : IEntityTypeConfiguration<InventoryPosition>
{
    public void Configure(EntityTypeBuilder<InventoryPosition> builder)
    {
        builder.ToTable("inventory_positions", t =>
            t.HasCheckConstraint("ck_inventory_positions_quantity", "\"quantity\" >= 0"));
        builder.Property(x => x.LocationKind).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Quantity).HasPrecision(12, 3);
        builder.HasIndex(x => new { x.BranchId, x.LocationKind, x.LocationId, x.VariantId }).IsUnique();
        builder.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Variant).WithMany().HasForeignKey(x => x.VariantId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class InventoryMovementConfiguration : IEntityTypeConfiguration<InventoryMovement>
{
    public void Configure(EntityTypeBuilder<InventoryMovement> builder)
    {
        builder.ToTable("inventory_movements", t =>
            t.HasCheckConstraint("ck_inventory_movements_quantity", "\"quantity\" > 0"));
        builder.Property(x => x.Quantity).HasPrecision(12, 3);
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.FromLocationKind).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ToLocationKind).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.SourceType).HasMaxLength(40);
        builder.HasIndex(x => new { x.SourceType, x.SourceId });
        builder.HasIndex(x => new { x.BranchId, x.OccurredAt });
        builder.HasIndex(x => new { x.ToLocationKind, x.ToLocationId, x.VariantId });
        builder.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Variant).WithMany().HasForeignKey(x => x.VariantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
