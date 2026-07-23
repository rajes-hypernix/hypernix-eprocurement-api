using FSH.Framework.Core.Exceptions;
using FSH.Modules.Platform.Contracts.v1.Countries;
using FSH.Modules.Platform.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.Countries.DeleteCountry;

public sealed class DeleteCountryCommandHandler(PlatformDbContext dbContext)
    : ICommandHandler<DeleteCountryCommand, Guid>
{
    public async ValueTask<Guid> Handle(DeleteCountryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var country = await dbContext.Countries.FirstOrDefaultAsync(c => c.Id == command.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Country {command.Id} not found.");

        country.Delete();
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return country.Id;
    }
}
