using FSH.Framework.Core.Exceptions;
using FSH.Modules.Platform.Contracts.v1.States;
using FSH.Modules.Platform.Data;
using FSH.Modules.Platform.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.States.UpdateState;

public sealed class UpdateStateCommandHandler(PlatformDbContext dbContext)
    : ICommandHandler<UpdateStateCommand, Guid>
{
    public async ValueTask<Guid> Handle(UpdateStateCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var state = await dbContext.States.FirstOrDefaultAsync(s => s.Id == command.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"State {command.Id} not found.");

        var code = command.Code.Trim().ToUpperInvariant();
        bool duplicate = await dbContext.States
            .AnyAsync(s => s.Id != command.Id && s.CountryId == state.CountryId && s.Code == code, cancellationToken)
            .ConfigureAwait(false);
        if (duplicate)
            throw new PlatformRuleException($"State '{code}' already exists for this country.");

        state.Update(command.Code, command.Name);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return state.Id;
    }
}
