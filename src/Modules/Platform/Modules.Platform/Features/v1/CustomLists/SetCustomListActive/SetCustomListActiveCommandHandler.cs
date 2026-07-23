using FSH.Framework.Core.Exceptions;
using FSH.Modules.Platform.Contracts.v1.CustomLists;
using FSH.Modules.Platform.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.CustomLists.SetCustomListActive;

public sealed class SetCustomListActiveCommandHandler(PlatformDbContext dbContext)
    : ICommandHandler<SetCustomListActiveCommand, Guid>
{
    public async ValueTask<Guid> Handle(SetCustomListActiveCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var list = await dbContext.CustomLists.FirstOrDefaultAsync(l => l.Id == command.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Custom list {command.Id} not found.");

        list.SetActive(command.IsActive);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return list.Id;
    }
}
