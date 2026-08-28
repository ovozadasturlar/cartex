using System.Text.Json;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Persistence.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Cartex.Persistence.Interceptors;

public sealed class EntityAuditInterceptor(ICurrentUser currentUser, AuditScopeState auditScope) : SaveChangesInterceptor
{
    private static readonly HashSet<Type> ExcludedTypes =
    [
        typeof(AuditLog),
        typeof(NotificationOutbox),
        typeof(NotificationDelivery),
        typeof(NotificationDeliveryAttempt),
        typeof(DebtReminderLog),
        typeof(RefreshSession),
        typeof(OtpChallenge),
        // Heartbeats and replay journals are high-volume operational records.
        // Claim/release and the replayed business command emit semantic audit events.
        typeof(OfflineAuthorityLease),
        typeof(OfflineSyncEvent),
        typeof(InventoryMovement)
    ];

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Capture(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Capture(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Capture(DbContext? context)
    {
        if (context is null || !currentUser.IsAuthenticated)
            return;

        var entries = context.ChangeTracker.Entries()
            .Where(x => x.Entity is BaseEntity
                && x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted
                && !ExcludedTypes.Contains(x.Entity.GetType()))
            .ToList();

        foreach (var entry in entries)
        {
            var (action, oldData, newData) = Snapshot(entry);
            if (oldData.Count == 0 && newData.Count == 0)
                continue;

            var entity = (BaseEntity)entry.Entity;
            var table = entry.Metadata.GetTableName() ?? entry.Entity.GetType().Name;
            var branchId = entry.Entity is IBranchScoped branchScoped ? branchScoped.BranchId : currentUser.DefaultBranchId;
            if (auditScope.IsActive)
            {
                auditScope.Capture(new AuditEntityChange(action, table, entity, branchId, oldData, newData));
                continue;
            }

            context.Set<AuditLog>().Add(new AuditLog
            {
                UserId = currentUser.UserId,
                Client = currentUser.Client,
                DeviceId = currentUser.DeviceId,
                DeviceName = currentUser.DeviceName,
                IpAddress = currentUser.IpAddress,
                UserAgent = currentUser.UserAgent,
                CorrelationId = currentUser.CorrelationId,
                Action = action,
                TableName = table,
                RecordId = entity.Id > 0 ? entity.Id : null,
                OldData = oldData.Count == 0 ? null : JsonSerializer.Serialize(oldData),
                NewData = newData.Count == 0 ? null : JsonSerializer.Serialize(newData),
                Details = JsonSerializer.Serialize(new
                {
                    changes = new[] { new { action, subjectType = table, subjectId = entity.Id > 0 ? entity.Id : (long?)null, oldValues = oldData, newValues = newData } }
                }),
                EntityCount = 1,
                BranchId = branchId
            });
        }
    }

    private static (string Action, Dictionary<string, object?> Old, Dictionary<string, object?> New) Snapshot(EntityEntry entry)
    {
        var oldData = new Dictionary<string, object?>();
        var newData = new Dictionary<string, object?>();
        var deleted = entry.State == EntityState.Deleted
            || entry.Properties.Any(p => p.Metadata.Name == nameof(ISoftDeletable.IsDeleted)
                && p.IsModified
                && p.CurrentValue is true);

        foreach (var property in entry.Properties)
        {
            if (property.Metadata.IsPrimaryKey())
                continue;
            if (entry.State == EntityState.Modified && !property.IsModified)
                continue;

            var name = property.Metadata.Name;
            var redact = IsSensitive(entry.Entity, name);
            var oldValue = redact ? "[REDACTED]" : Normalize(property.OriginalValue);
            var newValue = redact ? "[REDACTED]" : Normalize(property.CurrentValue);

            if (entry.State != EntityState.Added)
                oldData[name] = oldValue;
            if (entry.State != EntityState.Deleted)
                newData[name] = newValue;
        }

        var action = entry.State == EntityState.Added
            ? "EntityCreated"
            : deleted ? "EntityDeleted" : "EntityUpdated";
        return (action, oldData, newData);
    }

    private static object? Normalize(object? value) => value switch
    {
        null => null,
        byte[] bytes => $"[BINARY:{bytes.Length}]",
        string text when text.Length > 1000 => $"{text[..1000]}…[TRUNCATED:{text.Length}]",
        _ => value
    };

    private static bool IsSensitive(object entity, string propertyName)
    {
        if (entity is BusinessSetting && propertyName == nameof(BusinessSetting.Value))
            return true;

        return propertyName.Contains("Password", StringComparison.OrdinalIgnoreCase)
            || propertyName.Contains("Token", StringComparison.OrdinalIgnoreCase)
            || propertyName.Contains("Secret", StringComparison.OrdinalIgnoreCase)
            || propertyName.Contains("Hash", StringComparison.OrdinalIgnoreCase)
            || propertyName.Contains("ConnectionString", StringComparison.OrdinalIgnoreCase);
    }
}
