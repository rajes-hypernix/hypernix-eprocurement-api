using FSH.Framework.Core.Exceptions;
using FSH.Modules.Platform.Contracts.v1.Countries;
using FSH.Modules.Platform.Data;
using FSH.Modules.Platform.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.Countries.UpdateCountry;

public sealed class UpdateCountryCommandHandler(PlatformDbContext dbContext)
    : ICommandHandler<UpdateCountryCommand, Guid>
{
    public async ValueTask<Guid> Handle(UpdateCountryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var country = await dbContext.Countries.FirstOrDefaultAsync(c => c.Id == command.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Country {command.Id} not found.");

        var code = command.Code.Trim().ToUpperInvariant();
        bool duplicate = await dbContext.Countries
            .AnyAsync(c => c.Id != command.Id && c.Code == code, cancellationToken)
            .ConfigureAwait(false);
        if (duplicate)
            throw new PlatformRuleException($"Country '{code}' already exists.");

        country.Update(command.Code, command.Name);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return country.Id;
    }
}
