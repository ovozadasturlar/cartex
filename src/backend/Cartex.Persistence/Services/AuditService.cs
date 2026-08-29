using System.Text.Json;
using System.Text.RegularExpressions;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;

namespace Cartex.Persistence.Services;

public sealed partial class AuditService(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    AuditScopeState state) : IAuditService
{
    private const int MaxDetailsLength = 64 * 1024;

    public AuditCommandScope BeginCommand(string commandName) => state.Begin(commandName);

    public async Task CompleteCommandAsync(AuditCommandScope scope, CancellationToken cancellationToken = default)
    {
        if (!scope.IsRoot)
        {
            state.Complete(scope);
            return;
        }

        var snapshot = state.Snapshot(scope);
        if (snapshot.Primary is not null || snapshot.RelatedEvents.Count > 0 || snapshot.Changes.Count > 0)
        {
            db.AuditLogs.Add(Build(snapshot));
            await db.SaveChangesAsync(cancellationToken);
        }

        state.Complete(scope);
    }

    public void AbortCommand(AuditCommandScope scope) => state.Abort(scope);

    public void SetOutcome(
        string eventCode,
        string subjectType,
        long? subjectId,
        object? details = null,
        string? summary = null,
        long? branchId = null,
        long? asUserId = null)
    {
        var semanticEvent = Event(eventCode, subjectType, subjectId, details, summary, branchId, asUserId);
        if (state.IsActive)
        {
            state.SetPrimary(semanticEvent);
            return;
        }

        db.AuditLogs.Add(BuildStandalone(semanticEvent));
    }

    public void Add(
        string action,
        string table,
        long? recordId,
        object? newData = null,
        object? oldData = null,
        long? asUserId = null)
    {
        var semanticEvent = Event(action, table, recordId, newData, null, null, asUserId);
        if (state.IsActive)
        {
            state.AddEvent(semanticEvent);
            return;
        }

        var log = BuildStandalone(semanticEvent);
        log.OldData = Serialize(oldData);
        log.NewData = Serialize(newData);
        db.AuditLogs.Add(log);
    }

    private AuditLog Build(AuditScopeSnapshot snapshot)
    {
        var primary = snapshot.Primary;
        var singleChange = snapshot.Changes.Count == 1 ? snapshot.Changes[0] : null;
        var action = primary?.EventCode ?? $"command.{ToSnakeCase(WithoutCommandSuffix(snapshot.CommandName))}";
        var subjectType = primary?.SubjectType ?? singleChange?.SubjectType ?? "command";
        var subjectId = primary?.SubjectId ?? singleChange?.SubjectId;
        var branchId = primary?.BranchId
            ?? snapshot.Changes.Select(x => x.BranchId).FirstOrDefault(x => x.HasValue)
            ?? currentUser.DefaultBranchId;

        var details = Serialize(new
        {
            command = snapshot.CommandName,
            events = snapshot.RelatedEvents.Select(x => new
            {
                code = x.EventCode,
                subjectType = x.SubjectType,
                subjectId = x.SubjectId,
                summary = x.Summary,
                details = Element(x.DetailsJson)
            }),
            changes = snapshot.Changes.Select(x => new
            {
                action = x.Action,
                subjectType = x.SubjectType,
                subjectId = x.SubjectId,
                oldValues = x.OldValues,
                newValues = x.NewValues
            })
        });

        if (details is { Length: > MaxDetailsLength })
        {
            details = Serialize(new
            {
                command = snapshot.CommandName,
                truncated = true,
                events = snapshot.RelatedEvents.Select(x => new { code = x.EventCode, x.SubjectType, x.SubjectId }),
                changes = snapshot.Changes.Select(x => new { x.Action, x.SubjectType, x.SubjectId }),
                entityCount = snapshot.Changes.Count
            });
        }

        return Stamp(new AuditLog
        {
            UserId = primary?.AsUserId ?? currentUser.UserId,
            Action = Limit(action, 80) ?? "unknown",
            TableName = Limit(subjectType, 80) ?? "unknown",
            RecordId = subjectId,
            Summary = Limit(primary?.Summary ?? Humanize(snapshot.CommandName), 300),
            CommandName = Limit(snapshot.CommandName, 120),
            OldData = singleChange is null ? null : Serialize(singleChange.OldValues),
            NewData = singleChange is null ? primary?.DetailsJson : Serialize(singleChange.NewValues),
            Details = details,
            EntityCount = snapshot.Changes.Count,
            BranchId = branchId
        });
    }

    private AuditLog BuildStandalone(AuditSemanticEvent semanticEvent) => Stamp(new AuditLog
    {
        UserId = semanticEvent.AsUserId ?? currentUser.UserId,
        Action = Limit(semanticEvent.EventCode, 80) ?? "unknown",
        TableName = Limit(semanticEvent.SubjectType, 80) ?? "unknown",
        RecordId = semanticEvent.SubjectId,
        Summary = Limit(semanticEvent.Summary, 300),
        NewData = semanticEvent.DetailsJson,
        Details = semanticEvent.DetailsJson,
        BranchId = semanticEvent.BranchId ?? currentUser.DefaultBranchId
    });

    private AuditLog Stamp(AuditLog log)
    {
        log.Client = currentUser.Client;
        log.DeviceId = Limit(currentUser.DeviceId, 64);
        log.DeviceName = Limit(currentUser.DeviceName, 200);
        log.IpAddress = Limit(currentUser.IpAddress, 64);
        log.UserAgent = Limit(currentUser.UserAgent, 500);
        log.CorrelationId = Limit(currentUser.CorrelationId, 100);
        return log;
    }

    private static AuditSemanticEvent Event(
        string eventCode,
        string subjectType,
        long? subjectId,
        object? details,
        string? summary,
        long? branchId,
        long? asUserId = null) => new(
            Limit(eventCode.Trim(), 80) ?? "unknown",
            Limit(subjectType.Trim(), 80) ?? "unknown",
            subjectId,
            Serialize(details),
            Limit(summary, 300),
            branchId,
            asUserId);

    private static JsonElement? Element(string? json) =>
        json is null ? null : JsonSerializer.Deserialize<JsonElement>(json);

    private static string? Serialize(object? value) => value is null ? null : JsonSerializer.Serialize(value);

    private static string WithoutCommandSuffix(string value) =>
        value.EndsWith("Command", StringComparison.Ordinal) ? value[..^"Command".Length] : value;

    private static string Humanize(string commandName) =>
        PascalBoundary().Replace(WithoutCommandSuffix(commandName), " $1").Trim();

    private static string ToSnakeCase(string value) =>
        PascalBoundary().Replace(value, "_$1").Trim().ToLowerInvariant();

    private static string? Limit(string? value, int maxLength) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Length <= maxLength ? value : value[..maxLength];

    [GeneratedRegex("(?<!^)([A-Z])", RegexOptions.None, matchTimeoutMilliseconds: 200)]
    private static partial Regex PascalBoundary();
}
