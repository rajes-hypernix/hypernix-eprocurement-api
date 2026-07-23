using FSH.Framework.Core.Exceptions;
using FSH.Modules.Platform.Contracts.v1.States;
using FSH.Modules.Platform.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.States.SetStateActive;

public sealed class SetStateActiveCommandHandler(PlatformDbContext dbContext)
    : ICommandHandler<SetStateActiveCommand, Guid>
{
    public async ValueTask<Guid> Handle(SetStateActiveCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var state = await dbContext.States.FirstOrDefaultAsync(s => s.Id == command.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"State {command.Id} not found.");

        state.SetActive(command.IsActive);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return state.Id;
    }
}
