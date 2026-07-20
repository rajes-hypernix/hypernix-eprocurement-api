using Mediator;

namespace FSH.Modules.Sourcing.Contracts.v1.Requisitions;

/// <summary>[C7] Cannot cancel a PR while a line is in an RFQ or awarded.</summary>
public sealed record CancelRequisitionCommand(Guid RequisitionId) : ICommand<Guid>;
