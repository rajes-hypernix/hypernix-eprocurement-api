using FSH.Framework.Core.Exceptions;
using FSH.Modules.Platform.Contracts.v1.Countries;
using FSH.Modules.Platform.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.Countries.SetCountryActive;

public sealed class SetCountryActiveCommandHandler(PlatformDbContext dbContext)
    : ICommandHandler<SetCountryActiveCommand, Guid>
{
    public async ValueTask<Guid> Handle(SetCountryActiveCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var country = await dbContext.Countries.FirstOrDefaultAsync(c => c.Id == command.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Country {command.Id} not found.");

        country.SetActive(command.IsActive);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return country.Id;
    }
}
