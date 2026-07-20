using Mediator;

namespace FSH.Modules.Sourcing.Contracts.v1.Requisitions;

/// <summary>InDraftRfq -&gt; Open (draft RFQ abandoned before release).</summary>
public sealed record UnreserveRequisitionLineCommand(Guid RequisitionId, Guid LineId) : ICommand<Guid>;
