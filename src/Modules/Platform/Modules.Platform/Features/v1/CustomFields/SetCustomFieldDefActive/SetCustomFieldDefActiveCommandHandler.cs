using FSH.Framework.Core.Exceptions;
using FSH.Modules.Platform.Contracts.v1.CustomFields;
using FSH.Modules.Platform.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.CustomFields.SetCustomFieldDefActive;

public sealed class SetCustomFieldDefActiveCommandHandler(PlatformDbContext dbContext)
    : ICommandHandler<SetCustomFieldDefActiveCommand, Guid>
{
    public async ValueTask<Guid> Handle(SetCustomFieldDefActiveCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var def = await dbContext.CustomFieldDefs
            .FirstOrDefaultAsync(d => d.Id == command.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Custom field {command.Id} not found.");

        def.SetActive(command.IsActive);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return def.Id;
    }
}
