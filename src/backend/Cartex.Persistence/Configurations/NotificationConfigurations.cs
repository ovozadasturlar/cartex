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
        builder.Property(x => x.Purpose).HasMaxLength(60);
        builder.HasIndex(x => new { x.CustomerId, x.SentAt });
        builder.HasIndex(x => new { x.CustomerId, x.Purpose, x.DueDate });
    }
}

public class NotificationDeliveryConfiguration : IEntityTypeConfiguration<NotificationDelivery>
{
    public void Configure(EntityTypeBuilder<NotificationDelivery> builder)
    {
        builder.ToTable("notification_deliveries");
        builder.Property(x => x.Channel).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Purpose).HasMaxLength(60).IsRequired();
        builder.Property(x => x.Recipient).HasMaxLength(320).IsRequired();
        builder.Property(x => x.Subject).HasMaxLength(200);
        builder.Property(x => x.Content).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => new { x.Channel, x.Status, x.CreatedAt });
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class NotificationDeliveryAttemptConfiguration : IEntityTypeConfiguration<NotificationDeliveryAttempt>
{
    public void Configure(EntityTypeBuilder<NotificationDeliveryAttempt> builder)
    {
        builder.ToTable("notification_delivery_attempts");
        builder.Property(x => x.Provider).HasMaxLength(120).IsRequired();
        builder.Property(x => x.ProviderMessageId).HasMaxLength(160);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ErrorCode).HasMaxLength(80);
        builder.Property(x => x.ErrorMessage).HasMaxLength(1000);
        builder.HasIndex(x => new { x.Provider, x.Status, x.StartedAt });
        builder.HasIndex(x => x.ProviderMessageId);
        builder.HasIndex(x => new { x.NotificationDeliveryId, x.AttemptNumber }).IsUnique();
        builder.HasOne(x => x.NotificationDelivery)
            .WithMany(x => x.Attempts)
            .HasForeignKey(x => x.NotificationDeliveryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

[Obsolete("Legacy SMS journal retained to preserve historical production data.")]
public class SmsMessageConfiguration : IEntityTypeConfiguration<SmsMessage>
{
    public void Configure(EntityTypeBuilder<SmsMessage> builder)
    {
        builder.ToTable("sms_messages");
        builder.Property(x => x.Phone).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Text).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.Provider).HasMaxLength(30).IsRequired();
        builder.Property(x => x.ProviderMessageId).HasMaxLength(60);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(15);
        builder.Property(x => x.Error).HasMaxLength(1000);
        builder.HasIndex(x => new { x.Status, x.CreatedAt });
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
