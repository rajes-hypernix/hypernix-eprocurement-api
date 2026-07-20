using Mediator;

namespace FSH.Modules.Sourcing.Contracts.v1.Rfqs;

/// <summary>Returns any sourced PR lines to Open and closes their PrLineSourcing links.</summary>
public sealed record CancelRfqCommand(Guid RfqId) : ICommand<Guid>;
