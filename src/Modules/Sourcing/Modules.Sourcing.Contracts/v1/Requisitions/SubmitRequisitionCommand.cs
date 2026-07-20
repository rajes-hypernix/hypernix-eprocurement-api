using Mediator;

namespace FSH.Modules.Sourcing.Contracts.v1.Requisitions;

public sealed record SubmitRequisitionCommand(Guid RequisitionId) : ICommand<Guid>;
