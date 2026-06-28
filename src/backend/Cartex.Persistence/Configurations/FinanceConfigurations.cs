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

        builder.HasIndex(x => new { x.CustomerId, x.Type })
            .IsUnique()
            .HasFilter("\"customer_id\" IS NOT NULL");

        builder.HasIndex(x => new { x.BranchId, x.Type })
            .IsUnique()
            .HasFilter("\"branch_id\" IS NOT NULL");
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
