namespace Cartex.Persistence;

public interface IAuditService
{
    AuditCommandScope BeginCommand(string commandName);
    Task CompleteCommandAsync(AuditCommandScope scope, CancellationToken cancellationToken = default);
    void AbortCommand(AuditCommandScope scope);

    void SetOutcome(
        string eventCode,
        string subjectType,
        long? subjectId,
        object? details = null,
        string? summary = null,
        long? branchId = null,
        long? asUserId = null);

    void Add(string action, string table, long? recordId, object? newData = null, object? oldData = null, long? asUserId = null);
}

public sealed record AuditCommandScope(
    bool IsRoot,
    int ChangeStart,
    int EventStart,
    object? PreviousPrimary);
