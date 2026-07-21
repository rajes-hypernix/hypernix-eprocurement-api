using Mediator;

namespace FSH.Modules.Sourcing.Contracts.v1.Rfqs;

public sealed record DeclineInvitationCommand(Guid RfqId, string ReasonCode, string? Note) : ICommand<Guid>;
