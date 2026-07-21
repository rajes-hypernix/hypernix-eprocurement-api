using Mediator;

namespace FSH.Modules.Sourcing.Contracts.v1.Evaluation;

public sealed record FinalizeTechnicalCommand(Guid RfqId) : ICommand<Guid>;
