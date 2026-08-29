using Cartex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cartex.Persistence.Configurations;

public sealed class SmsGatewayDeviceConfiguration : IEntityTypeConfiguration<SmsGatewayDevice>
{
    public void Configure(EntityTypeBuilder<SmsGatewayDevice> builder)
    {
        builder.ToTable("sms_gateway_devices", table =>
        {
            table.HasCheckConstraint("ck_sms_gateway_quota_reset_day", "quota_reset_day BETWEEN 1 AND 28");
            table.HasCheckConstraint("ck_sms_gateway_monthly_quota", "monthly_quota IS NULL OR monthly_quota >= 0");
            table.HasCheckConstraint("ck_sms_gateway_sent_period", "sent_this_period >= 0");
            table.HasCheckConstraint("ck_sms_gateway_max_hour", "max_per_hour BETWEEN 1 AND 300");
            table.HasCheckConstraint("ck_sms_gateway_min_interval", "min_interval_seconds >= 2");
            table.HasCheckConstraint("ck_sms_gateway_consented_max_hour", "consented_max_per_hour BETWEEN 1 AND 300");
            table.HasCheckConstraint("ck_sms_gateway_consented_min_interval", "consented_min_interval_seconds >= 2");
            table.HasCheckConstraint("ck_sms_gateway_low_quota_warn", "low_quota_warn_percent BETWEEN 1 AND 100");
        });
        builder.Property(x => x.IsTrusted).HasDefaultValue(false);
        builder.Property(x => x.IsConsented).HasDefaultValue(false);
        builder.Property(x => x.IsEnabled).HasDefaultValue(false);
        builder.Property(x => x.QuotaResetDay).HasDefaultValue(1);
        builder.Property(x => x.MaxPerHour).HasDefaultValue(60);
        builder.Property(x => x.MinIntervalSeconds).HasDefaultValue(4);
        builder.Property(x => x.ConsentedMaxPerHour).HasDefaultValue(60);
        builder.Property(x => x.ConsentedMinIntervalSeconds).HasDefaultValue(4);
        builder.Property(x => x.LowQuotaWarnPercent).HasDefaultValue(10);
        builder.Property(x => x.DeviceId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.CredentialHash).HasMaxLength(128).IsRequired();
        builder.Property(x => x.DeviceName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Client).HasMaxLength(40).IsRequired();
        builder.Property(x => x.SimOperator).HasMaxLength(100).IsRequired();
        builder.Property(x => x.SimSubscriptionId).HasMaxLength(100).IsRequired();
        builder.Property(x => x.PhoneLabel).HasMaxLength(30);
        builder.Property(x => x.LastError).HasMaxLength(1000);
        builder.HasIndex(x => new { x.BranchId, x.DeviceId, x.SimSlot }).IsUnique();
        builder.HasIndex(x => new { x.BranchId, x.IsTrusted, x.IsConsented, x.IsEnabled, x.LastSeenAt });
        builder.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.LastUser).WithMany().HasForeignKey(x => x.LastUserId).OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class SmsGatewayJobConfiguration : IEntityTypeConfiguration<SmsGatewayJob>
{
    public void Configure(EntityTypeBuilder<SmsGatewayJob> builder)
    {
        builder.ToTable("sms_gateway_jobs", table =>
        {
            table.HasCheckConstraint("ck_sms_gateway_job_segments", "segment_count > 0");
        });
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Phone).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Text).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.LeaseToken).HasMaxLength(64);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(160).IsRequired();
        builder.Property(x => x.ErrorCode).HasMaxLength(80);
        builder.Property(x => x.ErrorMessage).HasMaxLength(1000);
        builder.Property(x => x.WaitingReason).HasMaxLength(80);
        builder.Property(x => x.FallbackProvider).HasMaxLength(30);
        builder.Property(x => x.FallbackMessageId).HasMaxLength(160);
        builder.HasIndex(x => x.IdempotencyKey).IsUnique();
        builder.HasIndex(x => new { x.BranchId, x.Status, x.CreatedAt });
        builder.HasIndex(x => new { x.AssignedDeviceId, x.Status });
        builder.HasIndex(x => new { x.BranchId, x.CustomerId, x.CreatedAt });
        builder.HasIndex(x => x.NotificationDeliveryAttemptId).IsUnique();
        builder.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.AssignedDevice).WithMany().HasForeignKey(x => x.AssignedDeviceId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.StickyDevice).WithMany().HasForeignKey(x => x.StickyDeviceId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.RetryOfJob).WithMany().HasForeignKey(x => x.RetryOfJobId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.NotificationDelivery).WithMany(x => x.SmsGatewayJobs)
            .HasForeignKey(x => x.NotificationDeliveryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.NotificationDeliveryAttempt).WithMany(x => x.SmsGatewayJobs)
            .HasForeignKey(x => x.NotificationDeliveryAttemptId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CustomerSmsRouteConfiguration : IEntityTypeConfiguration<CustomerSmsRoute>
{
    public void Configure(EntityTypeBuilder<CustomerSmsRoute> builder)
    {
        builder.ToTable("customer_sms_routes");
        builder.HasIndex(x => new { x.BranchId, x.CustomerId }).IsUnique();
        builder.HasIndex(x => x.LastDeviceId);
        builder.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.LastDevice).WithMany().HasForeignKey(x => x.LastDeviceId).OnDelete(DeleteBehavior.Cascade);
    }
}
