using Cartex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cartex.Persistence.Configurations;

public class HardwareKeyConfiguration : IEntityTypeConfiguration<HardwareKey>
{
    public void Configure(EntityTypeBuilder<HardwareKey> builder)
    {
        builder.ToTable("hardware_keys");
        builder.Property(x => x.Serial).HasMaxLength(100).IsRequired();
        builder.Property(x => x.IsEnabled).HasDefaultValue(true);
        builder.HasIndex(x => new { x.UserId, x.Serial });
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class RefreshSessionConfiguration : IEntityTypeConfiguration<RefreshSession>
{
    public void Configure(EntityTypeBuilder<RefreshSession> builder)
    {
        builder.ToTable("refresh_sessions");
        builder.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => x.UserId);
        builder.Property(x => x.DeviceName).HasMaxLength(200);
        builder.Property(x => x.ReplacedByHash).HasMaxLength(128);
        builder.Property(x => x.FamilyCreatedAt).HasDefaultValueSql("now()");
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
