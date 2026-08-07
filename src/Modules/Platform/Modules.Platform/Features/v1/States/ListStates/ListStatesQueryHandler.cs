using FSH.Modules.Platform.Contracts.Dtos;
using FSH.Modules.Platform.Contracts.v1.States;
using FSH.Modules.Platform.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.States.ListStates;

public sealed class ListStatesQueryHandler(PlatformDbContext dbContext)
    : IQueryHandler<ListStatesQuery, IReadOnlyList<StateDto>>
{
    public async ValueTask<IReadOnlyList<StateDto>> Handle(ListStatesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var q = dbContext.States.AsNoTracking().Where(s => s.CountryId == query.CountryId && !s.IsDeleted);
        if (query.ActiveOnly)
            q = q.Where(s => s.IsActive);

        return await q.OrderBy(s => s.Name)
            .Select(s => new StateDto(s.Id, s.CountryId, s.Code, s.Name, s.IsActive, s.CreatedOnUtc))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
