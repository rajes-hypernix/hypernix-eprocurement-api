using FSH.Framework.Core.Exceptions;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using FSH.Modules.Platform.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.Items.DeleteItem;

public sealed class DeleteItemCommandHandler(PlatformDbContext dbContext)
    : ICommandHandler<DeleteItemCommand, Guid>
{
    public async ValueTask<Guid> Handle(DeleteItemCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var item = await dbContext.Items
            .FirstOrDefaultAsync(i => i.Id == command.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Item {command.Id} not found.");

        dbContext.Items.Remove(item);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return command.Id;
    }
}
