using FSH.Framework.Core.Exceptions;
using FSH.Modules.Platform.Contracts.v1.Cities;
using FSH.Modules.Platform.Data;
using FSH.Modules.Platform.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.Cities.CreateCity;

public sealed class CreateCityCommandHandler(PlatformDbContext dbContext)
    : ICommandHandler<CreateCityCommand, Guid>
{
    public async ValueTask<Guid> Handle(CreateCityCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        bool stateExists = await dbContext.States.AnyAsync(s => s.Id == command.StateId, cancellationToken)
            .ConfigureAwait(false);
        if (!stateExists)
            throw new NotFoundException($"State {command.StateId} not found.");

        var name = command.Name.Trim();
        bool exists = await dbContext.Cities
            .AnyAsync(c => c.StateId == command.StateId && c.Name == name, cancellationToken)
            .ConfigureAwait(false);
        if (exists)
            throw new PlatformRuleException($"City '{name}' already exists for this state.");

        var city = City.Create(command.StateId, command.Name);
        dbContext.Cities.Add(city);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return city.Id;
    }
}
