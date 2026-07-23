using FSH.Framework.Core.Exceptions;
using FSH.Modules.Platform.Contracts.v1.Cities;
using FSH.Modules.Platform.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.Cities.SetCityActive;

public sealed class SetCityActiveCommandHandler(PlatformDbContext dbContext)
    : ICommandHandler<SetCityActiveCommand, Guid>
{
    public async ValueTask<Guid> Handle(SetCityActiveCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var city = await dbContext.Cities.FirstOrDefaultAsync(c => c.Id == command.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"City {command.Id} not found.");

        city.SetActive(command.IsActive);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return city.Id;
    }
}
