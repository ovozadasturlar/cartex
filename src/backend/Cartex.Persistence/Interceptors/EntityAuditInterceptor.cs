using System.Text.Json;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Cartex.Persistence.Interceptors;

public sealed class EntityAuditInterceptor(ICurrentUser currentUser) : SaveChangesInterceptor
{
    private static readonly HashSet<Type> ExcludedTypes =
    [
        typeof(AuditLog),
        typeof(NotificationOutbox),
        typeof(NotificationDelivery),
        typeof(NotificationDeliveryAttempt),
        typeof(DebtReminderLog),
        typeof(RefreshSession),
        typeof(OtpChallenge)
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
            context.Set<AuditLog>().Add(new AuditLog
            {
                UserId = currentUser.UserId,
                Client = currentUser.Client,
                Action = action,
                TableName = entry.Metadata.GetTableName() ?? entry.Entity.GetType().Name,
                RecordId = entity.Id > 0 ? entity.Id : null,
                OldData = oldData.Count == 0 ? null : JsonSerializer.Serialize(oldData),
                NewData = newData.Count == 0 ? null : JsonSerializer.Serialize(newData)
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
            var oldValue = redact ? "[REDACTED]" : property.OriginalValue;
            var newValue = redact ? "[REDACTED]" : property.CurrentValue;

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
