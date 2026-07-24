using Mediator;

namespace FSH.Modules.Sourcing.Contracts.v1.Requisitions;

public sealed record ReserveRequisitionQuantityLineInput(Guid PrLineId, decimal Qty);

/// <summary>
/// Internal, cross-module command (called via <see cref="IMediator"/> from Procurement when
/// creating a direct-from-requisition purchase order — not a public HTTP endpoint). Atomically
/// checks and claims quantity against one or more Open PR lines belonging to the same requisition:
/// the IC14 cap ("requisitioned minus already-ordered") enforced under a row lock on the parent
/// requisition, entirely inside Sourcing's own transaction, so concurrent direct-order requests
/// against the same requisition can't both pass. Throws a <c>SourcingRuleException</c> if any
/// line's requested quantity exceeds its remaining balance, or if a line isn't Open.
/// </summary>
public sealed record ReserveRequisitionQuantityCommand(
    Guid PrId,
    Guid PoId,
    IReadOnlyList<ReserveRequisitionQuantityLineInput> Lines) : ICommand<IReadOnlyList<Guid>>;
