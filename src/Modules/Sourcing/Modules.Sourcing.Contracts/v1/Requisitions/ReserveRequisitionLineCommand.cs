using Mediator;

namespace FSH.Modules.Sourcing.Contracts.v1.Requisitions;

/// <summary>Open -&gt; InDraftRfq (basket reservation into an RFQ draft workspace).</summary>
public sealed record ReserveRequisitionLineCommand(Guid RequisitionId, Guid LineId) : ICommand<Guid>;
