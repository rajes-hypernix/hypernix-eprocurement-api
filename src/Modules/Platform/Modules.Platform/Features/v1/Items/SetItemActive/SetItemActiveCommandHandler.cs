using FSH.Framework.Core.Exceptions;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using FSH.Modules.Platform.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.Items.SetItemActive;

public sealed class SetItemActiveCommandHandler(PlatformDbContext dbContext)
    : ICommandHandler<SetItemActiveCommand, Guid>
{
    public async ValueTask<Guid> Handle(SetItemActiveCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var item = await dbContext.Items
            .FirstOrDefaultAsync(i => i.Id == command.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Item {command.Id} not found.");

        item.SetActive(command.IsActive);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return item.Id;
    }
}
