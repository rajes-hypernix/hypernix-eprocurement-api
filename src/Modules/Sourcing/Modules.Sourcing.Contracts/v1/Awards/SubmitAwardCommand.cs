using FSH.Modules.Sourcing.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Sourcing.Contracts.v1.Awards;

/// <summary>CreatedByUserId is never a client-supplied field — it's derived server-side from the caller's user id.</summary>
public sealed record SubmitAwardCommand(Guid RfqId, IReadOnlyList<AwardAllocationDto> Allocations) : ICommand<AwardDto>;
