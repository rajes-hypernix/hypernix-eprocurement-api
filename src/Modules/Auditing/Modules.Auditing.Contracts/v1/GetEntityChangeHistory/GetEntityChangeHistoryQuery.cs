using FSH.Modules.Auditing.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Auditing.Contracts.v1.GetEntityChangeHistory;

/// <summary>
/// On-demand entity change history for a single aggregate id (not part of list endpoints).
/// </summary>
public sealed class GetEntityChangeHistoryQuery : IQuery<IReadOnlyList<AuditDetailDto>>
{
    public required Guid EntityId { get; init; }

    /// <summary>Max events to return (newest first). Default 50, capped at 200.</summary>
    public int Take { get; init; } = 50;
}
