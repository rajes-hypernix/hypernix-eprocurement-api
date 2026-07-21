using FSH.Modules.Platform.Contracts.v1.Countries;
using FSH.Modules.Platform.Data;
using FSH.Modules.Platform.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.Countries.CreateCountry;

public sealed class CreateCountryCommandHandler(PlatformDbContext dbContext)
    : ICommandHandler<CreateCountryCommand, Guid>
{
    public async ValueTask<Guid> Handle(CreateCountryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var code = command.Code.Trim().ToUpperInvariant();
        bool exists = await dbContext.Countries.AnyAsync(c => c.Code == code, cancellationToken).ConfigureAwait(false);
        if (exists)
            throw new PlatformRuleException($"Country '{code}' already exists.");

        var country = Country.Create(command.Code, command.Name);
        dbContext.Countries.Add(country);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return country.Id;
    }
}
