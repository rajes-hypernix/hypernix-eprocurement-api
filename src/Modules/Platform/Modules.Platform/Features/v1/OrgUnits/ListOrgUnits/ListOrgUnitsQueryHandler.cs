using FSH.Modules.Platform.Contracts.Dtos;
using FSH.Modules.Platform.Contracts.v1.OrgUnits;
using FSH.Modules.Platform.Data;
using FSH.Modules.Platform.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.OrgUnits.ListOrgUnits;

public sealed class ListOrgUnitsQueryHandler(PlatformDbContext dbContext)
    : IQueryHandler<ListOrgUnitsQuery, IReadOnlyList<OrgUnitDto>>
{
    public async ValueTask<IReadOnlyList<OrgUnitDto>> Handle(ListOrgUnitsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var q = dbContext.OrgUnits.AsNoTracking().AsQueryable();
        if (query.ActiveOnly)
            q = q.Where(u => u.IsActive);
        if (!string.IsNullOrWhiteSpace(query.Type)
            && Enum.TryParse<OrgUnitType>(query.Type, ignoreCase: true, out var type))
        {
            q = q.Where(u => u.Type == type);
        }

        var units = await q.OrderBy(u => u.Type).ThenBy(u => u.Code).ToListAsync(cancellationToken).ConfigureAwait(false);
        return [.. units.Select(PlatformDtoMapper.ToDto)];
    }
}
