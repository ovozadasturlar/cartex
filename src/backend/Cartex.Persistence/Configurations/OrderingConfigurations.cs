using Cartex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cartex.Persistence.Configurations;

public class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> builder)
    {
        builder.ToTable("carts");
        builder.Property(x => x.AggregateCode).HasMaxLength(40).IsRequired();
        builder.HasIndex(x => x.AggregateCode).IsUnique();
        builder.Property(x => x.IdempotencyKey).HasMaxLength(64);
        builder.HasIndex(x => x.IdempotencyKey).IsUnique()
            .HasFilter("idempotency_key IS NOT NULL");
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(15);
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(10);
        builder.Property(x => x.Version).IsConcurrencyToken().HasDefaultValue(1);
        builder.Property(x => x.CancellationReason).HasMaxLength(500);
        builder.Property(x => x.PaidCash).HasPrecision(14, 2);
        builder.Property(x => x.PaidCard).HasPrecision(14, 2);
        builder.Property(x => x.PaidBonus).HasPrecision(14, 2);
        builder.Property(x => x.CreditAmount).HasPrecision(14, 2);
        builder.Property(x => x.DebtCurrency).HasMaxLength(3);
        builder.Property(x => x.UseCustomerAdvance).HasDefaultValue(true);
        builder.HasIndex(x => new { x.Kind, x.Status });
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
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ClaimedByUser).WithMany().HasForeignKey(x => x.ClaimedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CancelledByUser).WithMany().HasForeignKey(x => x.CancelledByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Sale).WithMany().HasForeignKey(x => x.SaleId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.RequeuedFromCart).WithMany().HasForeignKey(x => x.RequeuedFromCartId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.SaleId).IsUnique().HasFilter("\"sale_id\" IS NOT NULL");
        builder.HasIndex(x => x.RequeuedFromCartId);
        builder.HasIndex(x => new { x.Status, x.ClaimedByUserId, x.ClaimedAt });
    }
}

public sealed class CartPaymentConfiguration : IEntityTypeConfiguration<CartPayment>
{
    public void Configure(EntityTypeBuilder<CartPayment> builder)
    {
        builder.ToTable("cart_payments", t =>
            t.HasCheckConstraint("ck_cart_payments_amount", "\"amount\" > 0"));
        builder.Property(x => x.Method).HasConversion<string>().HasMaxLength(15);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.Amount).HasPrecision(18, 4);
        builder.HasIndex(x => new { x.CartId, x.Method, x.Currency });
        builder.HasOne(x => x.Cart).WithMany(x => x.Payments)
            .HasForeignKey(x => x.CartId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
        builder.ToTable("cart_items");
        builder.Property(x => x.Quantity).HasPrecision(12, 3);
        builder.Property(x => x.UnitPriceOverride).HasPrecision(18, 4);

        builder.HasOne(x => x.Cart)
            .WithMany(c => c.Items)
            .HasForeignKey(x => x.CartId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Variant)
            .WithMany()
            .HasForeignKey(x => x.VariantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
