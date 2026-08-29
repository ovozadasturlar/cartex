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
    string? Client,
    Guid EventId = default,
    string? Summary = null,
    string? CommandName = null,
    string? Details = null,
    int EntityCount = 0,
    long? BranchId = null,
    string? DeviceId = null,
    string? DeviceName = null,
    string? IpAddress = null,
    string? CorrelationId = null);
