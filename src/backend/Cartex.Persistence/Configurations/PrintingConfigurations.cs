using Cartex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cartex.Persistence.Configurations;

public class PrintNodeConfiguration : IEntityTypeConfiguration<PrintNode>
{
    public void Configure(EntityTypeBuilder<PrintNode> builder)
    {
        builder.ToTable("print_nodes");
        builder.Property(x => x.DeviceId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.CredentialHash).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ClientVersion).HasMaxLength(40);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.LastClient).HasMaxLength(20);
        builder.Property(x => x.LastIpAddress).HasMaxLength(64);
        builder.HasIndex(x => x.DeviceId).IsUnique();
        builder.HasIndex(x => new { x.BranchId, x.IsEnabled, x.Status });
        builder.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.LastUser).WithMany().HasForeignKey(x => x.LastUserId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class PrinterEndpointConfiguration : IEntityTypeConfiguration<PrinterEndpoint>
{
    public void Configure(EntityTypeBuilder<PrinterEndpoint> builder)
    {
        builder.ToTable("printer_endpoints");
        builder.Property(x => x.StableKey).HasMaxLength(256).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.SystemName).HasMaxLength(260).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ProfileJson).HasColumnType("jsonb");
        builder.HasIndex(x => new { x.PrintNodeId, x.StableKey }).IsUnique();
        builder.HasIndex(x => new { x.IsEnabled, x.Status });
        builder.HasOne(x => x.PrintNode).WithMany(x => x.Endpoints).HasForeignKey(x => x.PrintNodeId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PrintRoutingPolicyConfiguration : IEntityTypeConfiguration<PrintRoutingPolicy>
{
    public void Configure(EntityTypeBuilder<PrintRoutingPolicy> builder)
    {
        builder.ToTable("print_routing_policies");
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.RoutingMode).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.StickyMode).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.ReceiptSettingsOverrideJson).HasColumnType("jsonb");
        builder.Property(x => x.Revision).IsConcurrencyToken();
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("ck_print_routing_default_copies", "default_copies BETWEEN 1 AND 100");
            table.HasCheckConstraint("ck_print_routing_revision", "revision > 0");
        });
        builder.HasIndex(x => new { x.BranchId, x.Kind }).IsUnique();
        builder.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.StickyEndpoint).WithMany().HasForeignKey(x => x.StickyEndpointId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class PrintRouteTargetConfiguration : IEntityTypeConfiguration<PrintRouteTarget>
{
    public void Configure(EntityTypeBuilder<PrintRouteTarget> builder)
    {
        builder.ToTable("print_route_targets");
        builder.HasIndex(x => new { x.PrintRoutingPolicyId, x.PrinterEndpointId }).IsUnique();
        builder.HasIndex(x => new { x.PrintRoutingPolicyId, x.Priority });
        builder.HasOne(x => x.PrintRoutingPolicy).WithMany(x => x.Targets).HasForeignKey(x => x.PrintRoutingPolicyId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.PrinterEndpoint).WithMany().HasForeignKey(x => x.PrinterEndpointId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PrintRequesterDeviceConfiguration : IEntityTypeConfiguration<PrintRequesterDevice>
{
    public void Configure(EntityTypeBuilder<PrintRequesterDevice> builder)
    {
        builder.ToTable("print_requester_devices");
        builder.Property(x => x.DeviceId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Client).HasMaxLength(20);
        builder.Property(x => x.LastIpAddress).HasMaxLength(64);
        builder.HasIndex(x => new { x.BranchId, x.DeviceId }).IsUnique();
        builder.HasIndex(x => new { x.BranchId, x.IsTrusted });
        builder.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.LastUser).WithMany().HasForeignKey(x => x.LastUserId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class PrintJobConfiguration : IEntityTypeConfiguration<PrintJob>
{
    public void Configure(EntityTypeBuilder<PrintJob> builder)
    {
        builder.ToTable("print_jobs");
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.SourceType).HasMaxLength(30).IsRequired();
        builder.Property(x => x.SourceId).HasMaxLength(160).IsRequired();
        builder.Property(x => x.PayloadJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.IdempotencyKey).HasMaxLength(128);
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.Property(x => x.RequestedDeviceId).HasMaxLength(64);
        builder.Property(x => x.RequestedDeviceName).HasMaxLength(200);
        builder.Property(x => x.RequestedClient).HasMaxLength(20);
        builder.Property(x => x.RequestedIpAddress).HasMaxLength(64);
        builder.Property(x => x.RequestedUserAgent).HasMaxLength(500);
        builder.Property(x => x.CorrelationId).HasMaxLength(80);
        builder.Property(x => x.LeaseToken).HasMaxLength(64);
        builder.Property(x => x.ErrorCode).HasMaxLength(80);
        builder.Property(x => x.ErrorMessage).HasMaxLength(1000);
        builder.HasIndex(x => new { x.BranchId, x.Kind, x.Status, x.CreatedAt });
        builder.HasIndex(x => new { x.AssignedNodeId, x.Status });
        builder.HasIndex(x => new { x.BranchId, x.Kind, x.IdempotencyKey }).IsUnique();
        builder.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.RequestedByUser).WithMany().HasForeignKey(x => x.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.OriginNode).WithMany().HasForeignKey(x => x.OriginNodeId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.AssignedNode).WithMany().HasForeignKey(x => x.AssignedNodeId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.AssignedEndpoint).WithMany().HasForeignKey(x => x.AssignedEndpointId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class PrintAttemptConfiguration : IEntityTypeConfiguration<PrintAttempt>
{
    public void Configure(EntityTypeBuilder<PrintAttempt> builder)
    {
        builder.ToTable("print_attempts");
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.LeaseToken).HasMaxLength(64).IsRequired();
        builder.Property(x => x.SpoolJobId).HasMaxLength(160);
        builder.Property(x => x.ErrorCode).HasMaxLength(80);
        builder.Property(x => x.ErrorMessage).HasMaxLength(1000);
        builder.HasIndex(x => new { x.PrintJobId, x.AttemptNumber }).IsUnique();
        builder.HasIndex(x => new { x.PrinterEndpointId, x.Status, x.StartedAt });
        builder.HasOne(x => x.PrintJob).WithMany(x => x.Attempts).HasForeignKey(x => x.PrintJobId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.PrintNode).WithMany().HasForeignKey(x => x.PrintNodeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PrinterEndpoint).WithMany().HasForeignKey(x => x.PrinterEndpointId).OnDelete(DeleteBehavior.Restrict);
    }
}
