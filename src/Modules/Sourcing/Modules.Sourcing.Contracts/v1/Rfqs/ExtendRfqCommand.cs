using Mediator;

namespace FSH.Modules.Sourcing.Contracts.v1.Rfqs;

/// <summary>Open-only, forward-only, future-only, capped at a configured max extensions.</summary>
public sealed record ExtendRfqCommand(Guid RfqId, DateTime NewClosesUtc, string? ReasonCode, string? Note) : ICommand<Guid>;
