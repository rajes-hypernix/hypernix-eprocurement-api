using FSH.Modules.Procurement.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Procurement.Contracts.v1.Asns;

/// <summary>Optional <paramref name="PoId"/> filters to ASNs for one purchase order.</summary>
public sealed record ListAsnsQuery(Guid? PoId = null) : IQuery<IReadOnlyList<AsnDto>>;
