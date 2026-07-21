using FSH.Modules.Sourcing.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Sourcing.Contracts.v1.Evaluation;

/// <summary>EvaluatorId is never a client-supplied field — it's derived server-side from the caller's user id.</summary>
public sealed record SetScoreCommand(Guid RfqId, Guid VendorId, string Criterion, int Score) : ICommand<TechnicalScoreDto>;
