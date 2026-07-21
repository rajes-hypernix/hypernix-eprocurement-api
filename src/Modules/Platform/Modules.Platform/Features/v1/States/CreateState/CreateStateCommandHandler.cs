using FSH.Framework.Core.Exceptions;
using FSH.Modules.Platform.Contracts.v1.States;
using FSH.Modules.Platform.Data;
using FSH.Modules.Platform.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.States.CreateState;

public sealed class CreateStateCommandHandler(PlatformDbContext dbContext)
    : ICommandHandler<CreateStateCommand, Guid>
{
    public async ValueTask<Guid> Handle(CreateStateCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        bool countryExists = await dbContext.Countries.AnyAsync(c => c.Id == command.CountryId, cancellationToken)
            .ConfigureAwait(false);
        if (!countryExists)
            throw new NotFoundException($"Country {command.CountryId} not found.");

        var code = command.Code.Trim().ToUpperInvariant();
        bool exists = await dbContext.States
            .AnyAsync(s => s.CountryId == command.CountryId && s.Code == code, cancellationToken)
            .ConfigureAwait(false);
        if (exists)
            throw new PlatformRuleException($"State '{code}' already exists for this country.");

        var state = State.Create(command.CountryId, command.Code, command.Name);
        dbContext.States.Add(state);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return state.Id;
    }
}
