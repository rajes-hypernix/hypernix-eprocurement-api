using Mediator;

namespace FSH.Modules.Sourcing.Contracts.v1.Rfqs;

/// <summary>An invitation with a submitted bid can never be rescinded.</summary>
public sealed record RescindInvitationCommand(Guid RfqId, Guid VendorId, string ReasonCode, string? Note) : ICommand<Guid>;
