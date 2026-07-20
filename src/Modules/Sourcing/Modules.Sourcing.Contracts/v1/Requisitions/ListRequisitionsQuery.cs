using FSH.Modules.Sourcing.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Sourcing.Contracts.v1.Requisitions;

public sealed record ListRequisitionsQuery : IQuery<IReadOnlyList<RequisitionListItemDto>>;
