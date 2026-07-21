using Mediator;

namespace FSH.Modules.Sourcing.Contracts.v1.Evaluation;

public sealed record OpenTechnicalEnvelopeCommand(Guid RfqId) : ICommand<Guid>;
