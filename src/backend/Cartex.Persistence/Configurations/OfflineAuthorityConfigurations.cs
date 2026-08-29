using Cartex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cartex.Persistence.Configurations;

public sealed class OfflineAuthorityLeaseConfiguration : IEntityTypeConfiguration<OfflineAuthorityLease>
{
    public void Configure(EntityTypeBuilder<OfflineAuthorityLease> builder)
    {
        builder.ToTable("offline_authority_leases");
        builder.Property(x => x.DeviceId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.DeviceName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.RevokeReason).HasMaxLength(500);
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.HasIndex(x => x.BusinessId).IsUnique()
            .HasFilter("\"revoked_at\" IS NULL");
        builder.HasIndex(x => new { x.DeviceId, x.RevokedAt });
        builder.HasIndex(x => new { x.WarehouseId, x.RevokedAt });
        builder.HasOne(x => x.Business).WithMany().HasForeignKey(x => x.BusinessId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.RevokedByUser).WithMany().HasForeignKey(x => x.RevokedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class OfflineSyncEventConfiguration : IEntityTypeConfiguration<OfflineSyncEvent>
{
    public void Configure(EntityTypeBuilder<OfflineSyncEvent> builder)
    {
        builder.ToTable("offline_sync_events");
        builder.Property(x => x.Kind).HasMaxLength(30).IsRequired();
        builder.Property(x => x.IdempotencyKey).HasMaxLength(128).IsRequired();
        builder.Property(x => x.PayloadHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.ResultCode).HasMaxLength(100);
        builder.HasIndex(x => x.EventId).IsUnique();
        builder.HasIndex(x => new { x.OfflineAuthorityLeaseId, x.Sequence }).IsUnique();
        builder.HasIndex(x => new { x.OfflineAuthorityLeaseId, x.ProcessedAt });
        builder.HasOne(x => x.Lease).WithMany(x => x.Events)
            .HasForeignKey(x => x.OfflineAuthorityLeaseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ActorUser).WithMany()
            .HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
