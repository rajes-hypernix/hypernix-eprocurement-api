using eProcure.Application.Abstractions;
using eProcure.Domain;
using eProcure.Infrastructure.Persistence;

namespace eProcure.Infrastructure.Services;

/// <summary>
/// Appends an immutable <see cref="AuditEntry"/>, stamping who (from
/// <see cref="ICurrentUser"/>) and when (from <see cref="IClock"/>) automatically
/// (BUSINESS-RULES [A]).
/// </summary>
public sealed class AuditLogWriter(AppDbContext db, IClock clock, ICurrentUser user) : IAuditLog
{
    public Task WriteAsync(
        string entityType,
        string entityId,
        string action,
        string? before = null,
        string? after = null,
        CancellationToken ct = default)
        => Append(entityType, entityId, action, before, after, null, null, null, ct);

    public Task WriteTransitionAsync(
        string entityType,
        string entityId,
        string action,
        string? fromState,
        string? toState,
        string? reason = null,
        CancellationToken ct = default)
        => Append(entityType, entityId, action, before: null, after: null, fromState, toState, reason, ct);

    private async Task Append(
        string entityType, string entityId, string action,
        string? before, string? after, string? fromState, string? toState, string? reason,
        CancellationToken ct)
    {
        var entry = new AuditEntry(
            entityType,
            entityId,
            action,
            before,
            after,
            actorId: user.UserId ?? "system",
            actorName: user.UserName ?? "System",
            utcTimestamp: clock.UtcNow,
            fromState: fromState,
            toState: toState,
            reason: reason);

        db.AuditEntries.Add(entry);
        await db.SaveChangesAsync(ct);
    }
}
