using FSH.Modules.Procurement.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Procurement.Contracts.v1.Grns;

public sealed record GetGrnByAsnQuery(Guid AsnId) : IQuery<GrnDto?>;
