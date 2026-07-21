using FSH.Modules.Procurement.Contracts.Dtos;
using FSH.Modules.Procurement.Contracts.v1.Asns;
using FSH.Modules.Procurement.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Procurement.Features.v1.Asns.GetAsn;

public sealed class GetAsnQueryHandler(ProcurementDbContext dbContext)
    : IQueryHandler<GetAsnQuery, AsnDto?>
{
    public async ValueTask<AsnDto?> Handle(GetAsnQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var asn = await dbContext.Asns
            .AsNoTracking()
            .Include(a => a.Lines)
            .FirstOrDefaultAsync(a => a.Id == query.AsnId, cancellationToken)
            .ConfigureAwait(false);

        return asn is null ? null : ProcurementDtoMapper.ToDto(asn);
    }
}
