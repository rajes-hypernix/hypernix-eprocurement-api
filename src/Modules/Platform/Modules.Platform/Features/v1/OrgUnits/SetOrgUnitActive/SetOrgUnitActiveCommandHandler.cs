using FSH.Framework.Core.Exceptions;
using FSH.Modules.Platform.Contracts.v1.OrgUnits;
using FSH.Modules.Platform.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.OrgUnits.SetOrgUnitActive;

public sealed class SetOrgUnitActiveCommandHandler(PlatformDbContext dbContext)
    : ICommandHandler<SetOrgUnitActiveCommand, Guid>
{
    public async ValueTask<Guid> Handle(SetOrgUnitActiveCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var unit = await dbContext.OrgUnits.FirstOrDefaultAsync(u => u.Id == command.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Org unit {command.Id} not found.");

        unit.SetActive(command.IsActive);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return unit.Id;
    }
}
