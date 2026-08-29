using Cartex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cartex.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");
        builder.HasIndex(x => x.CreatedAt);
        builder.Property(x => x.EventId).HasDefaultValueSql("gen_random_uuid()");
        builder.HasIndex(x => x.EventId).IsUnique();
        builder.HasIndex(x => new { x.Action, x.CreatedAt });
        builder.HasIndex(x => new { x.TableName, x.RecordId, x.CreatedAt });
        builder.HasIndex(x => x.CorrelationId);
        builder.Property(x => x.Action).HasMaxLength(80);
        builder.Property(x => x.TableName).HasMaxLength(80);
        builder.Property(x => x.Summary).HasMaxLength(300);
        builder.Property(x => x.CommandName).HasMaxLength(120);
        builder.Property(x => x.Client).HasMaxLength(30);
        builder.Property(x => x.DeviceId).HasMaxLength(64);
        builder.Property(x => x.DeviceName).HasMaxLength(200);
        builder.Property(x => x.IpAddress).HasMaxLength(64);
        builder.Property(x => x.UserAgent).HasMaxLength(500);
        builder.Property(x => x.CorrelationId).HasMaxLength(100);
        builder.Property(x => x.OldData).HasColumnType("jsonb");
        builder.Property(x => x.NewData).HasColumnType("jsonb");
        builder.Property(x => x.Details).HasColumnType("jsonb");
    }
}
