using FSH.Framework.Core.Exceptions;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using FSH.Modules.Platform.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.Locations.SetLocationActive;

public sealed class SetLocationActiveCommandHandler(PlatformDbContext dbContext)
    : ICommandHandler<SetLocationActiveCommand, Guid>
{
    public async ValueTask<Guid> Handle(SetLocationActiveCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var location = await dbContext.Locations
            .FirstOrDefaultAsync(l => l.Id == command.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Location {command.Id} not found.");

        location.SetActive(command.IsActive);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return location.Id;
    }
}
