using FSH.Modules.Sourcing.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Sourcing.Contracts.v1.Requisitions;

public sealed record GetRequisitionByIdQuery(Guid RequisitionId) : IQuery<RequisitionDto>;
