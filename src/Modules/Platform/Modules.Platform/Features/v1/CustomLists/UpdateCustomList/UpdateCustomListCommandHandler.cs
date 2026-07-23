using FSH.Framework.Core.Exceptions;
using FSH.Modules.Platform.Contracts.v1.CustomLists;
using FSH.Modules.Platform.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.CustomLists.UpdateCustomList;

public sealed class UpdateCustomListCommandHandler(PlatformDbContext dbContext)
    : ICommandHandler<UpdateCustomListCommand, Guid>
{
    public async ValueTask<Guid> Handle(UpdateCustomListCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var list = await dbContext.CustomLists.FirstOrDefaultAsync(l => l.Id == command.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Custom list {command.Id} not found.");

        list.Update(command.Name);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return list.Id;
    }
}
