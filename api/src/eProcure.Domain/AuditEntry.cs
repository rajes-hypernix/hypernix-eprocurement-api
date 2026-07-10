namespace eProcure.Domain;

/// <summary>
/// Immutable, append-only record of a state change. Written on every
/// transition across sourcing/eval/award and procure-to-pay (BUSINESS-RULES [A]).
/// Never updated or deleted after creation.
/// </summary>
public class AuditEntry
{
    public Guid Id { get; private set; }
    public string EntityType { get; private set; } = default!;
    public string EntityId { get; private set; } = default!;
    public string Action { get; private set; } = default!;
    public string? Before { get; private set; }
    public string? After { get; private set; }

    // Typed transition columns (DATA-MODEL-ANALYTICS §1) — structured signal for cycle-time
    // analytics ("submitted → sourced → awarded in N days"), not a free-text blob.
    public string? FromState { get; private set; }
    public string? ToState { get; private set; }
    public string? Reason { get; private set; }

    public string ActorId { get; private set; } = default!;
    public string ActorName { get; private set; } = default!;
    public DateTime UtcTimestamp { get; private set; }

    // EF Core
    private AuditEntry() { }

    public AuditEntry(
        string entityType,
        string entityId,
        string action,
        string? before,
        string? after,
        string actorId,
        string actorName,
        DateTime utcTimestamp,
        string? fromState = null,
        string? toState = null,
        string? reason = null)
    {
        Id = Guid.NewGuid();
        EntityType = entityType;
        EntityId = entityId;
        Action = action;
        Before = before;
        After = after;
        FromState = fromState;
        ToState = toState;
        Reason = reason;
        ActorId = actorId;
        ActorName = actorName;
        UtcTimestamp = utcTimestamp;
    }
}
