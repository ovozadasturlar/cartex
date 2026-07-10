using System.Text.Json;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;

namespace Cartex.Persistence.Services;

public sealed class AuditService(IApplicationDbContext db, ICurrentUser currentUser) : IAuditService
{
    public void Add(string action, string table, long? recordId, object? newData = null, object? oldData = null, long? asUserId = null) =>
        db.AuditLogs.Add(new AuditLog
        {
            UserId = asUserId ?? currentUser.UserId,
            Client = currentUser.Client,
            Action = action,
            TableName = table,
            RecordId = recordId,
            NewData = newData is null ? null : JsonSerializer.Serialize(newData),
            OldData = oldData is null ? null : JsonSerializer.Serialize(oldData)
        });
}
