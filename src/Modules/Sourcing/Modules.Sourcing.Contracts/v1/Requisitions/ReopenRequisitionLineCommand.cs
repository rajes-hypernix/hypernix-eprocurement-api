using Mediator;

namespace FSH.Modules.Sourcing.Contracts.v1.Requisitions;

public sealed record ReopenRequisitionLineCommand(Guid RequisitionId, Guid LineId) : ICommand<Guid>;
