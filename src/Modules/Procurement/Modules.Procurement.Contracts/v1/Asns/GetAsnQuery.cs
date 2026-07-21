using FSH.Modules.Procurement.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Procurement.Contracts.v1.Asns;

public sealed record GetAsnQuery(Guid AsnId) : IQuery<AsnDto?>;
