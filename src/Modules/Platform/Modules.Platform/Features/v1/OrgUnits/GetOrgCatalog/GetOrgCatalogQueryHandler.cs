using FSH.Modules.Platform.Contracts.Dtos;
using FSH.Modules.Platform.Contracts.v1.OrgUnits;
using FSH.Modules.Platform.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.OrgUnits.GetOrgCatalog;

public sealed class GetOrgCatalogQueryHandler(PlatformDbContext dbContext)
    : IQueryHandler<GetOrgCatalogQuery, OrgCatalogDto>
{
    public async ValueTask<OrgCatalogDto> Handle(GetOrgCatalogQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var q = dbContext.OrgUnits.AsNoTracking().AsQueryable();
        if (query.ActiveOnly)
            q = q.Where(u => u.IsActive);

        var units = await q.OrderBy(u => u.Type).ThenBy(u => u.Code).ToListAsync(cancellationToken).ConfigureAwait(false);
        return new OrgCatalogDto([.. units.Select(PlatformDtoMapper.ToDto)]);
    }
}
