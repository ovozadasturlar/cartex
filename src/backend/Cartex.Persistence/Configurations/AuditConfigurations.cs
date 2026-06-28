using Cartex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cartex.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");
        builder.Property(x => x.Action).HasMaxLength(15);
        builder.Property(x => x.TableName).HasMaxLength(40);
        builder.Property(x => x.OldData).HasColumnType("jsonb");
        builder.Property(x => x.NewData).HasColumnType("jsonb");
    }
}
