using FSH.Framework.Core.Exceptions;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using FSH.Modules.Platform.Data;
using FSH.Modules.Platform.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.Items.UpdateItem;

public sealed class UpdateItemCommandHandler(PlatformDbContext dbContext)
    : ICommandHandler<UpdateItemCommand, Guid>
{
    public async ValueTask<Guid> Handle(UpdateItemCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var item = await dbContext.Items
            .FirstOrDefaultAsync(i => i.Id == command.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Item {command.Id} not found.");

        var itemCode = command.ItemCode.Trim().ToUpperInvariant();
        bool duplicate = await dbContext.Items
            .AnyAsync(i => i.Id != command.Id && i.ItemCode == itemCode, cancellationToken)
            .ConfigureAwait(false);
        if (duplicate)
            throw new PlatformRuleException($"Item '{itemCode}' already exists.");

        item.Update(command.ItemCode, command.Description, command.Uom);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return item.Id;
    }
}
