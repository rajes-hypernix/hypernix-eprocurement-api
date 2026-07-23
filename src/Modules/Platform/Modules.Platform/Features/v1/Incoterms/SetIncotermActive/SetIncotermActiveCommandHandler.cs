using FSH.Framework.Core.Exceptions;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using FSH.Modules.Platform.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.Incoterms.SetIncotermActive;

public sealed class SetIncotermActiveCommandHandler(PlatformDbContext dbContext)
    : ICommandHandler<SetIncotermActiveCommand, Guid>
{
    public async ValueTask<Guid> Handle(SetIncotermActiveCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var incoterm = await dbContext.Incoterms
            .FirstOrDefaultAsync(i => i.Id == command.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Incoterm {command.Id} not found.");

        incoterm.SetActive(command.IsActive);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return incoterm.Id;
    }
}
