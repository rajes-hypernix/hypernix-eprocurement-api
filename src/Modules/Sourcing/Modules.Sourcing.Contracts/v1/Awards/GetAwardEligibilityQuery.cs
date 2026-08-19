using FSH.Modules.Sourcing.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Sourcing.Contracts.v1.Awards;

public sealed record GetAwardEligibilityQuery(Guid RfqId) : IQuery<AwardEligibilityDto>;
