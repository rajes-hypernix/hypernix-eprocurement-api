using Mediator;

namespace FSH.Modules.Sourcing.Contracts.v1.Requisitions;

/// <summary>Releases an Open line for re-sourcing (a no-op state change that records the action — e.g. "no quotes").</summary>
public sealed record ReleaseRequisitionLineCommand(Guid RequisitionId, Guid LineId, string? Reason) : ICommand<Guid>;
