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

        builder.HasOne(x => x.Branch)
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.BranchId)
            .IsUnique()
            .HasFilter("\"branch_id\" IS NOT NULL");
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
