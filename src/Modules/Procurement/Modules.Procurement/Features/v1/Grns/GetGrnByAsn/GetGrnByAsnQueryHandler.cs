using FSH.Modules.Procurement.Contracts.Dtos;
using FSH.Modules.Procurement.Contracts.v1.Grns;
using FSH.Modules.Procurement.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Procurement.Features.v1.Grns.GetGrnByAsn;

public sealed class GetGrnByAsnQueryHandler(ProcurementDbContext dbContext)
    : IQueryHandler<GetGrnByAsnQuery, GrnDto?>
{
    public async ValueTask<GrnDto?> Handle(GetGrnByAsnQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var grn = await dbContext.Grns
            .AsNoTracking()
            .Include(g => g.Lines)
            .FirstOrDefaultAsync(g => g.AsnId == query.AsnId, cancellationToken)
            .ConfigureAwait(false);

        return grn is null ? null : ProcurementDtoMapper.ToDto(grn);
    }
}
