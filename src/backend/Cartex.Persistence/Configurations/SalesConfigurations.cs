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
        builder.Property(x => x.LastName).HasMaxLength(120);
        builder.Property(x => x.Address).HasMaxLength(250);
        builder.Property(x => x.Phone).HasMaxLength(20);
        builder.HasIndex(x => x.Phone).IsUnique().HasFilter("\"phone\" IS NOT NULL AND NOT \"is_deleted\"");
        builder.Property(x => x.Email).HasMaxLength(120);
        builder.Property(x => x.PreferredLanguage).HasMaxLength(10);
        builder.Property(x => x.CardBarcode).HasMaxLength(60);
        builder.HasIndex(x => x.CardBarcode).IsUnique().HasFilter("\"card_barcode\" IS NOT NULL AND NOT \"is_deleted\"");
        builder.Property(x => x.DiscountPct).HasPrecision(5, 2);
        builder.Property(x => x.CreditLimit).HasPrecision(18, 2);
        builder.HasIndex(x => x.AssignedUserId);
        builder.HasIndex(x => x.PartyId).IsUnique();
        builder.HasOne(x => x.Party).WithOne(x => x.CustomerProfile)
            .HasForeignKey<Customer>(x => x.PartyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AssignedUser).WithMany().HasForeignKey(x => x.AssignedUserId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> builder)
    {
        builder.ToTable("sales");
        builder.Property(x => x.TotalAmount).HasPrecision(18, 2);
        builder.Property(x => x.DiscountAmount).HasPrecision(18, 2);
        builder.Property(x => x.CashbackEarned).HasPrecision(18, 2);
        builder.Property(x => x.PaidCash).HasPrecision(18, 2);
        builder.Property(x => x.PaidCard).HasPrecision(18, 2);
        builder.Property(x => x.PaidBonus).HasPrecision(18, 2);
        builder.Property(x => x.PaidAdvance).HasPrecision(18, 2);
        builder.Property(x => x.DebtAmount).HasPrecision(18, 2);
        builder.Property(x => x.DebtCurrency).HasMaxLength(3);
        builder.Property(x => x.DebtRate).HasPrecision(18, 6);
        builder.Property(x => x.ChangeAmount).HasPrecision(18, 2);
        builder.Property(x => x.CreditAmount).HasPrecision(18, 2);
        builder.Property(x => x.RefundedCash).HasPrecision(18, 2);
        builder.Property(x => x.RefundedCard).HasPrecision(18, 2);
        builder.Property(x => x.RefundedBonus).HasPrecision(18, 2);
        builder.Property(x => x.RefundedDebt).HasPrecision(18, 2);
        builder.Property(x => x.RefundedAdvance).HasPrecision(18, 2);
        builder.Property(x => x.ReturnNoChargeAmount).HasPrecision(18, 2);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(15);
        builder.Property(x => x.DocumentNumber).HasMaxLength(40).IsRequired();
        // Raqamlashdan oldingi savdolar bo'sh qiymat bilan qoladi — unikallik faqat
        // haqiqiy raqamlarga tegishli, aks holda migratsiya to'ldirilgan bazada yiqiladi.
        builder.HasIndex(x => x.DocumentNumber).IsUnique().HasFilter("\"document_number\" <> ''");
        builder.Property(x => x.ReceiptToken).HasMaxLength(40).IsRequired();
        builder.HasIndex(x => x.ReceiptToken).IsUnique();
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.Property(x => x.VoidReason).HasMaxLength(500);
        builder.HasOne(x => x.Shift).WithMany().HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(x => x.ShiftId);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(64);
        builder.HasIndex(x => new { x.UserId, x.IdempotencyKey }).IsUnique().HasFilter("\"idempotency_key\" IS NOT NULL");
        builder.HasIndex(x => x.BranchId);
        builder.HasIndex(x => x.CreatedAt);

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
        builder.Property(x => x.DiscountAmount).HasPrecision(14, 2);
        builder.Property(x => x.PriceCurrency).HasMaxLength(3);
        builder.Property(x => x.PriceRate).HasPrecision(18, 6);
        builder.Property(x => x.PurchasePrice).HasPrecision(14, 2);
        builder.Property(x => x.CashbackEarned).HasPrecision(18, 2);
        builder.Property(x => x.ReturnedQuantity).HasPrecision(12, 3);
        builder.Property(x => x.ReturnedCashback).HasPrecision(18, 2);

        builder.HasOne(x => x.Sale)
            .WithMany(s => s.Items)
            .HasForeignKey(x => x.SaleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Variant)
            .WithMany()
            .HasForeignKey(x => x.VariantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Stock)
            .WithMany()
            .HasForeignKey(x => x.StockId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class SalePaymentConfiguration : IEntityTypeConfiguration<SalePayment>
{
    public void Configure(EntityTypeBuilder<SalePayment> builder)
    {
        builder.ToTable("sale_payments");
        builder.Property(x => x.Method).HasConversion<string>().HasMaxLength(10);
        builder.Property(x => x.Currency).HasMaxLength(3);
        builder.Property(x => x.Amount).HasPrecision(18, 4);
        builder.Property(x => x.Rate).HasPrecision(18, 6);
        builder.Property(x => x.AmountBase).HasPrecision(18, 2);

        builder.HasOne(x => x.Sale)
            .WithMany(s => s.Payments)
            .HasForeignKey(x => x.SaleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
