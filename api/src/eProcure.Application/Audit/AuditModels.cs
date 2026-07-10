namespace eProcure.Application.Audit;

public sealed record AuditEntryDto(
    string EntityType,
    string EntityId,
    string Action,
    string? Before,
    string? After,
    string ActorName,
    DateTime UtcTimestamp);

public interface IAuditQuery
{
    Task<IReadOnlyList<AuditEntryDto>> ListForAsync(string entityType, string entityId, CancellationToken ct = default);
}
