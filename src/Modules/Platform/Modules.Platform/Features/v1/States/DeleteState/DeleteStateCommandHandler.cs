using FSH.Framework.Core.Exceptions;
using FSH.Modules.Platform.Contracts.v1.States;
using FSH.Modules.Platform.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.States.DeleteState;

public sealed class DeleteStateCommandHandler(PlatformDbContext dbContext)
    : ICommandHandler<DeleteStateCommand, Guid>
{
    public async ValueTask<Guid> Handle(DeleteStateCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var state = await dbContext.States.FirstOrDefaultAsync(s => s.Id == command.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"State {command.Id} not found.");

        state.Delete();
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return state.Id;
    }
}
