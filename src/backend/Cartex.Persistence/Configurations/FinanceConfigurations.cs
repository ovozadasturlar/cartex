using Cartex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cartex.Persistence.Configurations;

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts");
        builder.Property(x => x.Name).HasMaxLength(60);
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Currency).HasMaxLength(3);
        builder.Property(x => x.Balance).HasPrecision(18, 2);

        builder.HasOne(x => x.Branch)
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Customer)
            .WithMany(c => c.Accounts)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Supplier)
            .WithMany(s => s.Accounts)
            .HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.CustomerId, x.Type, x.Currency })
            .IsUnique()
            .HasFilter("\"customer_id\" IS NOT NULL");

        builder.HasIndex(x => new { x.BranchId, x.Type, x.Currency })
            .IsUnique()
            .HasFilter("\"branch_id\" IS NOT NULL");

        builder.HasIndex(x => new { x.SupplierId, x.Type, x.Currency })
            .IsUnique()
            .HasFilter("\"supplier_id\" IS NOT NULL");
    }
}

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("transactions");
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasMaxLength(3);
        builder.Property(x => x.Rate).HasPrecision(18, 6);
        builder.Property(x => x.OperationType).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(64);
        builder.HasIndex(x => new { x.UserId, x.IdempotencyKey }).IsUnique().HasFilter("\"idempotency_key\" IS NOT NULL");
        builder.HasIndex(x => x.CreatedAt);

        builder.HasOne(x => x.FromAccount)
            .WithMany()
            .HasForeignKey(x => x.FromAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ToAccount)
            .WithMany()
            .HasForeignKey(x => x.ToAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Shift)
            .WithMany(s => s.Transactions)
            .HasForeignKey(x => x.ShiftId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ExpenseCategory)
            .WithMany()
            .HasForeignKey(x => x.ExpenseCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ShiftId);
    }
}

public class ExpenseCategoryConfiguration : IEntityTypeConfiguration<ExpenseCategory>
{
    public void Configure(EntityTypeBuilder<ExpenseCategory> builder)
    {
        builder.ToTable("expense_categories");
        builder.Property(x => x.Name).HasMaxLength(60).IsRequired();
    }
}

public class ShiftCashConfiguration : IEntityTypeConfiguration<ShiftCash>
{
    public void Configure(EntityTypeBuilder<ShiftCash> builder)
    {
        builder.ToTable("shift_cash");
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.OpeningFloat).HasPrecision(18, 2);
        builder.Property(x => x.CountedCash).HasPrecision(18, 2);
        builder.HasIndex(x => new { x.ShiftId, x.Currency }).IsUnique();

        builder.HasOne(x => x.Shift)
            .WithMany(s => s.CashRows)
            .HasForeignKey(x => x.ShiftId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ExchangeRateConfiguration : IEntityTypeConfiguration<ExchangeRate>
{
    public void Configure(EntityTypeBuilder<ExchangeRate> builder)
    {
        builder.ToTable("exchange_rates");
        builder.Property(x => x.Code).HasMaxLength(3).IsRequired();
        builder.Property(x => x.Rate).HasPrecision(18, 6);
        builder.Property(x => x.Source).HasMaxLength(20);
        builder.HasIndex(x => new { x.Code, x.EffectiveAt });
    }
}

public class ShiftConfiguration : IEntityTypeConfiguration<Shift>
{
    public void Configure(EntityTypeBuilder<Shift> builder)
    {
        builder.ToTable("shifts");
        builder.Property(x => x.OpeningFloat).HasPrecision(18, 2);
        builder.Property(x => x.CountedCash).HasPrecision(18, 2);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(15);
        builder.HasIndex(x => new { x.UserId, x.BranchId, x.Status });

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
