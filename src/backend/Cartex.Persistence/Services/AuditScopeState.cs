using Cartex.Domain.Common;

namespace Cartex.Persistence.Services;

public sealed record AuditEntityChange(
    string Action,
    string SubjectType,
    BaseEntity Entity,
    long? BranchId,
    IReadOnlyDictionary<string, object?> OldValues,
    IReadOnlyDictionary<string, object?> NewValues)
{
    public long? SubjectId => Entity.Id > 0 ? Entity.Id : null;
}

public sealed record AuditSemanticEvent(
    string EventCode,
    string SubjectType,
    long? SubjectId,
    string? DetailsJson,
    string? Summary,
    long? BranchId,
    long? AsUserId = null);

public sealed record AuditScopeSnapshot(
    string CommandName,
    AuditSemanticEvent? Primary,
    IReadOnlyList<AuditSemanticEvent> RelatedEvents,
    IReadOnlyList<AuditEntityChange> Changes);

public sealed class AuditScopeState
{
    private readonly List<AuditEntityChange> _changes = [];
    private readonly List<AuditSemanticEvent> _events = [];
    private int _depth;
    private string? _commandName;
    private AuditSemanticEvent? _primary;

    public bool IsActive => _depth > 0;

    public AuditCommandScope Begin(string commandName)
    {
        var isRoot = _depth == 0;
        if (isRoot)
        {
            Reset();
            _commandName = commandName;
        }

        var scope = new AuditCommandScope(isRoot, _changes.Count, _events.Count, _primary);
        _depth++;
        return scope;
    }

    public void Capture(AuditEntityChange change)
    {
        if (IsActive)
            _changes.Add(change);
    }

    public void AddEvent(AuditSemanticEvent semanticEvent)
    {
        if (!IsActive) return;
        _events.Add(semanticEvent);
        _primary ??= semanticEvent;
    }

    public void SetPrimary(AuditSemanticEvent semanticEvent)
    {
        if (!IsActive) return;
        if (_primary is not null && !_events.Contains(_primary))
            _events.Add(_primary);
        _primary = semanticEvent;
    }

    public AuditScopeSnapshot Snapshot(AuditCommandScope scope)
    {
        if (!scope.IsRoot || _depth != 1)
            throw new InvalidOperationException("Only the outer audit scope can be flushed.");

        var related = _events.Where(x => !ReferenceEquals(x, _primary)).ToList();
        return new AuditScopeSnapshot(_commandName ?? "UnknownCommand", _primary, related, [.. _changes]);
    }

    public void Complete(AuditCommandScope scope)
    {
        if (_depth <= 0) return;
        _depth--;
        if (scope.IsRoot)
            Reset();
    }

    public void Abort(AuditCommandScope scope)
    {
        if (_changes.Count > scope.ChangeStart)
            _changes.RemoveRange(scope.ChangeStart, _changes.Count - scope.ChangeStart);
        if (_events.Count > scope.EventStart)
            _events.RemoveRange(scope.EventStart, _events.Count - scope.EventStart);
        _primary = scope.PreviousPrimary as AuditSemanticEvent;

        if (_depth > 0) _depth--;
        if (scope.IsRoot)
            Reset();
    }

    private void Reset()
    {
        _depth = 0;
        _commandName = null;
        _primary = null;
        _changes.Clear();
        _events.Clear();
    }
}
