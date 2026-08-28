using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Sourcing.Contracts.Dtos;
using FSH.Modules.Sourcing.Contracts.v1.Awards;
using FSH.Modules.Sourcing.Data;
using FSH.Modules.Sourcing.Services;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Sourcing.Features.v1.Awards.GetAward;

public sealed class GetAwardQueryHandler(SourcingDbContext dbContext, ICurrentUser currentUser)
    : IQueryHandler<GetAwardQuery, AwardDto?>
{
    public async ValueTask<AwardDto?> Handle(GetAwardQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (currentUser.GetVendorId() is not null)
        {
            throw new ForbiddenException("Vendors cannot view award workspaces.");
        }

        var award = await dbContext.Awards
            .AsNoTracking()
                    .Include(a => a.Allocations)
                    .FirstOrDefaultAsync(a => a.RfqId == query.RfqId, cancellationToken)
            .ConfigureAwait(false);

        return award is null ? null : AwardDtoMapper.ToDto(award);
    }
}
