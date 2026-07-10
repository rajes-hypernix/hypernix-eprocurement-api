namespace eProcure.Application.Abstractions;

/// <summary>
/// Writes an immutable audit entry for a state transition. The actor (who) and
/// time (when) are resolved from <see cref="ICurrentUser"/> / <see cref="IClock"/>,
/// so callers supply only the what/before/after (BUSINESS-RULES [A]).
/// </summary>
public interface IAuditLog
{
    Task WriteAsync(
        string entityType,
        string entityId,
        string action,
        string? before = null,
        string? after = null,
        CancellationToken ct = default);

    /// <summary>
    /// Writes an audit entry carrying typed transition columns (from-state, to-state, reason)
    /// for cycle-time/state-machine analytics (DATA-MODEL-ANALYTICS §1). Additive overload —
    /// existing callers keep using the free-text form above.
    /// </summary>
    Task WriteTransitionAsync(
        string entityType,
        string entityId,
        string action,
        string? fromState,
        string? toState,
        string? reason = null,
        CancellationToken ct = default);
}
