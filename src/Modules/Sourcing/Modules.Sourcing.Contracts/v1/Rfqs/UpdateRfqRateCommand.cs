using Mediator;

namespace FSH.Modules.Sourcing.Contracts.v1.Rfqs;

/// <summary>Re-snapshots Platform current FX onto a Draft RFQ (units of base per 1 foreign).</summary>
public sealed record UpdateRfqRateCommand(Guid RfqId) : ICommand<Guid>;
