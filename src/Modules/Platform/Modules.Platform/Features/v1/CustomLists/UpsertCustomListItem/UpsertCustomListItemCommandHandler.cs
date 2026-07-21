using FSH.Framework.Core.Exceptions;
using FSH.Modules.Platform.Contracts.v1.CustomLists;
using FSH.Modules.Platform.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.CustomLists.UpsertCustomListItem;

public sealed class UpsertCustomListItemCommandHandler(PlatformDbContext dbContext)
    : ICommandHandler<UpsertCustomListItemCommand, Guid>
{
    public async ValueTask<Guid> Handle(UpsertCustomListItemCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var key = command.ListKey.Trim();
        var list = await dbContext.CustomLists
            .Include(l => l.Items)
            .FirstOrDefaultAsync(l => l.Key == key, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Custom list '{key}' not found.");

        var item = list.UpsertItem(command.Code, command.Label, command.SortOrder, command.IsActive);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return item.Id;
    }
}
