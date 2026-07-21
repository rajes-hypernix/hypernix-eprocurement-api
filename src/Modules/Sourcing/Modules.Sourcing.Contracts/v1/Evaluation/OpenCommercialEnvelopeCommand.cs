using Mediator;

namespace FSH.Modules.Sourcing.Contracts.v1.Evaluation;

public sealed record OpenCommercialEnvelopeCommand(Guid RfqId) : ICommand<Guid>;
