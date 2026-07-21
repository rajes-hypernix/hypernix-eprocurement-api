using FSH.Modules.Platform.Contracts.v1.CustomLists;
using FSH.Modules.Platform.Data;
using FSH.Modules.Platform.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.CustomLists.CreateCustomList;

public sealed class CreateCustomListCommandHandler(PlatformDbContext dbContext)
    : ICommandHandler<CreateCustomListCommand, Guid>
{
    public async ValueTask<Guid> Handle(CreateCustomListCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var key = command.Key.Trim();
        bool exists = await dbContext.CustomLists.AnyAsync(l => l.Key == key, cancellationToken).ConfigureAwait(false);
        if (exists)
            throw new PlatformRuleException($"Custom list '{key}' already exists.");

        var list = CustomList.Create(command.Key, command.Name);
        dbContext.CustomLists.Add(list);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return list.Id;
    }
}
