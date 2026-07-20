using Mediator;

namespace FSH.Modules.Sourcing.Contracts.v1.Rfqs;

public sealed record CloseRfqCommand(Guid RfqId) : ICommand<Guid>;
