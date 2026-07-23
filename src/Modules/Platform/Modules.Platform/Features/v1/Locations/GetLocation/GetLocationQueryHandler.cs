using FSH.Framework.Core.Exceptions;
using FSH.Modules.Platform.Contracts.Dtos;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using FSH.Modules.Platform.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.Locations.GetLocation;

public sealed class GetLocationQueryHandler(PlatformDbContext dbContext)
    : IQueryHandler<GetLocationQuery, LocationDto>
{
    public async ValueTask<LocationDto> Handle(GetLocationQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var location = await dbContext.Locations.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == query.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Location {query.Id} not found.");

        return ConfigurationMapping.ToDto(location);
    }
}
