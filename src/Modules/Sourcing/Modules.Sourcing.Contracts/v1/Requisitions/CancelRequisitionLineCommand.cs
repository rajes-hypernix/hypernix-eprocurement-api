using Mediator;

namespace FSH.Modules.Sourcing.Contracts.v1.Requisitions;

public sealed record CancelRequisitionLineCommand(Guid RequisitionId, Guid LineId, string? Reason) : ICommand<Guid>;
