using FSH.Modules.Sourcing.Contracts.Dtos;
using FSH.Modules.Sourcing.Contracts.v1.Awards;
using FSH.Modules.Sourcing.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Sourcing.Features.v1.Awards.GetAward;

public sealed class GetAwardQueryHandler(SourcingDbContext dbContext)
    : IQueryHandler<GetAwardQuery, AwardDto?>
{
    public async ValueTask<AwardDto?> Handle(GetAwardQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var award = await dbContext.Awards
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.RfqId == query.RfqId, cancellationToken)
            .ConfigureAwait(false);

        return award is null ? null : AwardDtoMapper.ToDto(award);
    }
}
