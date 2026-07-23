using FSH.Modules.Platform.Contracts.v1.Configuration;
using FSH.Modules.Platform.Data;
using FSH.Modules.Platform.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.Items.CreateItem;

public sealed class CreateItemCommandHandler(PlatformDbContext dbContext)
    : ICommandHandler<CreateItemCommand, Guid>
{
    public async ValueTask<Guid> Handle(CreateItemCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var itemCode = command.ItemCode.Trim().ToUpperInvariant();
        bool exists = await dbContext.Items
            .AnyAsync(i => i.ItemCode == itemCode, cancellationToken)
            .ConfigureAwait(false);
        if (exists)
            throw new PlatformRuleException($"Item '{itemCode}' already exists.");

        var item = Item.Create(command.ItemCode, command.Description, command.Uom);
        dbContext.Items.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return item.Id;
    }
}
