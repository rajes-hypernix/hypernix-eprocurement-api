using FSH.Modules.Procurement.Contracts.Dtos;
using FSH.Modules.Procurement.Contracts.v1.Asns;
using FSH.Modules.Procurement.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Procurement.Features.v1.Asns.ListAsns;

public sealed class ListAsnsQueryHandler(ProcurementDbContext dbContext)
    : IQueryHandler<ListAsnsQuery, IReadOnlyList<AsnDto>>
{
    public async ValueTask<IReadOnlyList<AsnDto>> Handle(ListAsnsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var q = dbContext.Asns.AsNoTracking().Include(a => a.Lines).AsQueryable();
        if (query.PoId is Guid poId)
        {
            q = q.Where(a => a.PoId == poId);
        }

        var asns = await q
            .OrderByDescending(a => a.CreatedUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. asns.Select(ProcurementDtoMapper.ToDto)];
    }
}
