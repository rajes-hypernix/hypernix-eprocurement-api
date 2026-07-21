using FSH.Modules.Sourcing.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Sourcing.Contracts.v1.Evaluation;

public sealed record GetTechnicalEvalQuery(Guid RfqId) : IQuery<IReadOnlyList<TechnicalEvalRowDto>>;
