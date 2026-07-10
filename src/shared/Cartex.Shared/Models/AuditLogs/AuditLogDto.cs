namespace Cartex.Shared.Models.AuditLogs;

public record AuditLogDto(
    long Id,
    string? UserName,
    string Action,
    string TableName,
    long? RecordId,
    string? OldData,
    string? NewData,
    DateTime CreatedAt,
    string? Client);
