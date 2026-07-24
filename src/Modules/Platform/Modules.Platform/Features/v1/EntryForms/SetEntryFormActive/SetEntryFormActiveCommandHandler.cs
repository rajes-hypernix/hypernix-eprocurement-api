using FSH.Framework.Core.Exceptions;
using FSH.Modules.Platform.Contracts.v1.EntryForms;
using FSH.Modules.Platform.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.EntryForms.SetEntryFormActive;

public sealed class SetEntryFormActiveCommandHandler(PlatformDbContext dbContext)
    : ICommandHandler<SetEntryFormActiveCommand, Guid>
{
    public async ValueTask<Guid> Handle(SetEntryFormActiveCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var form = await dbContext.EntryFormDefs
            .FirstOrDefaultAsync(f => f.Id == command.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Entry form {command.Id} not found.");

        form.SetActive(command.IsActive);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return form.Id;
    }
}
