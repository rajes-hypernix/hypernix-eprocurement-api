using FSH.Modules.Sourcing.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Sourcing.Contracts.v1.Rfqs;

public sealed record GetRfqByIdQuery(Guid RfqId) : IQuery<RfqDetailDto>;
