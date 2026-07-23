using FSH.Framework.Core.Exceptions;
using FSH.Modules.Platform.Contracts.v1.Cities;
using FSH.Modules.Platform.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.Cities.DeleteCity;

public sealed class DeleteCityCommandHandler(PlatformDbContext dbContext)
    : ICommandHandler<DeleteCityCommand, Guid>
{
    public async ValueTask<Guid> Handle(DeleteCityCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var city = await dbContext.Cities.FirstOrDefaultAsync(c => c.Id == command.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"City {command.Id} not found.");

        city.Delete();
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return city.Id;
    }
}
