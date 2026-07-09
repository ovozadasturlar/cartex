using Cartex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cartex.Persistence.Configurations;

public class LoyaltyProgramConfiguration : IEntityTypeConfiguration<LoyaltyProgram>
{
    public void Configure(EntityTypeBuilder<LoyaltyProgram> builder)
    {
        builder.ToTable("loyalty_programs");
        builder.Property(x => x.TotalPercent).HasPrecision(5, 2);
        builder.Property(x => x.DiscountCombineMode).HasConversion<string>().HasMaxLength(20).HasDefaultValue(Cartex.Domain.Enums.DiscountCombineMode.Priority);

        builder.HasOne(x => x.Branch)
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.BranchId)
            .IsUnique()
            .HasFilter("\"branch_id\" IS NOT NULL");
    }
}

public class DiscountRuleConfiguration : IEntityTypeConfiguration<DiscountRule>
{
    public void Configure(EntityTypeBuilder<DiscountRule> builder)
    {
        builder.ToTable("discount_rules");
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Scope).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Method).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Value).HasPrecision(14, 2);
        builder.Property(x => x.MinAmount).HasPrecision(14, 2);

        builder.HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.Scope, x.TargetId });
        builder.HasIndex(x => x.CustomerId);
    }
}

public class DiscountRuleExceptionConfiguration : IEntityTypeConfiguration<DiscountRuleException>
{
    public void Configure(EntityTypeBuilder<DiscountRuleException> builder)
    {
        builder.ToTable("discount_rule_exceptions");
        builder.Property(x => x.Scope).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(x => x.DiscountRule)
            .WithMany(r => r.Exceptions)
            .HasForeignKey(x => x.DiscountRuleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.DiscountRuleId, x.Scope, x.TargetId }).IsUnique();
    }
}

public class ManufacturerConfiguration : IEntityTypeConfiguration<Manufacturer>
{
    public void Configure(EntityTypeBuilder<Manufacturer> builder)
    {
        builder.ToTable("manufacturers");
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.Name).IsUnique().HasFilter("NOT \"is_deleted\"");
    }
}

public class CashbackRuleConfiguration : IEntityTypeConfiguration<CashbackRule>
{
    public void Configure(EntityTypeBuilder<CashbackRule> builder)
    {
        builder.ToTable("cashback_rules");
        builder.Property(x => x.Scope).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Method).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Value).HasPrecision(14, 2);

        builder.HasOne(x => x.LoyaltyProgram)
            .WithMany(p => p.Rules)
            .HasForeignKey(x => x.LoyaltyProgramId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.LoyaltyProgramId, x.Scope, x.TargetId });
    }
}
