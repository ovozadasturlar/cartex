using Cartex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cartex.Persistence.Configurations;

public class DebtReminderLogConfiguration : IEntityTypeConfiguration<DebtReminderLog>
{
    public void Configure(EntityTypeBuilder<DebtReminderLog> builder)
    {
        builder.ToTable("debt_reminder_log");
        builder.Property(x => x.Balance).HasPrecision(18, 2);
        builder.HasIndex(x => new { x.CustomerId, x.SentAt });
    }
}

public class NotificationOutboxConfiguration : IEntityTypeConfiguration<NotificationOutbox>
{
    public void Configure(EntityTypeBuilder<NotificationOutbox> builder)
    {
        builder.ToTable("notification_outbox");
        builder.Property(x => x.EventType).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Payload).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(15);
        builder.Property(x => x.Error).HasMaxLength(1000);
        builder.HasIndex(x => new { x.Status, x.OccurredAt });
    }
}
