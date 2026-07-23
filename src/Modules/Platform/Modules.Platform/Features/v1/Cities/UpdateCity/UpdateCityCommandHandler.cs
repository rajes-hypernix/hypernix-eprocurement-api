using FSH.Framework.Core.Exceptions;
using FSH.Modules.Platform.Contracts.v1.Cities;
using FSH.Modules.Platform.Data;
using FSH.Modules.Platform.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.Cities.UpdateCity;

public sealed class UpdateCityCommandHandler(PlatformDbContext dbContext)
    : ICommandHandler<UpdateCityCommand, Guid>
{
    public async ValueTask<Guid> Handle(UpdateCityCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var city = await dbContext.Cities.FirstOrDefaultAsync(c => c.Id == command.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"City {command.Id} not found.");

        var name = command.Name.Trim();
        bool duplicate = await dbContext.Cities
            .AnyAsync(c => c.Id != command.Id && c.StateId == city.StateId && c.Name == name, cancellationToken)
            .ConfigureAwait(false);
        if (duplicate)
            throw new PlatformRuleException($"City '{name}' already exists for this state.");

        city.Update(command.Name);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return city.Id;
    }
}
