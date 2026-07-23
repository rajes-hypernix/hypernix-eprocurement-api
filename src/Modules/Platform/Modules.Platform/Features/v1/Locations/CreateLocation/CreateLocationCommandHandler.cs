using FSH.Modules.Platform.Contracts.v1.Configuration;
using FSH.Modules.Platform.Data;
using FSH.Modules.Platform.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.Locations.CreateLocation;

public sealed class CreateLocationCommandHandler(PlatformDbContext dbContext)
    : ICommandHandler<CreateLocationCommand, Guid>
{
    public async ValueTask<Guid> Handle(CreateLocationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var code = command.Code.Trim().ToUpperInvariant();
        bool exists = await dbContext.Locations
            .AnyAsync(l => l.Code == code, cancellationToken)
            .ConfigureAwait(false);
        if (exists)
            throw new PlatformRuleException($"Location '{code}' already exists.");

        var location = Location.Create(command.Code, command.Name, ConfigurationMapping.ToDrafts(command.Addresses));
        dbContext.Locations.Add(location);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return location.Id;
    }
}
