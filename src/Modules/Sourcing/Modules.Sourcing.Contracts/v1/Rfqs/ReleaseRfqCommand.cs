using Mediator;

namespace FSH.Modules.Sourcing.Contracts.v1.Rfqs;

/// <summary>Draft -&gt; Open. Requires at least one invited vendor and a close date. Writes PrLineSourcing lineage.</summary>
public sealed record ReleaseRfqCommand(Guid RfqId) : ICommand<Guid>;
