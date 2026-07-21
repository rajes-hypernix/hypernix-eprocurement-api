using FSH.Modules.Platform.Contracts.v1.OrgUnits;
using FSH.Modules.Platform.Data;
using FSH.Modules.Platform.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.OrgUnits.CreateOrgUnit;

public sealed class CreateOrgUnitCommandHandler(PlatformDbContext dbContext)
    : ICommandHandler<CreateOrgUnitCommand, Guid>
{
    public async ValueTask<Guid> Handle(CreateOrgUnitCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!Enum.TryParse<OrgUnitType>(command.Type, ignoreCase: true, out var type))
            throw new PlatformRuleException($"Unknown org unit type '{command.Type}'.");

        if (command.ParentId is Guid parentId)
        {
            bool parentExists = await dbContext.OrgUnits.AnyAsync(u => u.Id == parentId, cancellationToken).ConfigureAwait(false);
            if (!parentExists)
                throw new PlatformRuleException($"Parent org unit {parentId} not found.");
        }

        var code = command.Code.Trim().ToUpperInvariant();
        bool exists = await dbContext.OrgUnits.AnyAsync(u => u.Type == type && u.Code == code, cancellationToken)
            .ConfigureAwait(false);
        if (exists)
            throw new PlatformRuleException($"Org unit '{code}' already exists for type {type}.");

        var unit = OrgUnit.Create(command.Code, command.Name, type, command.ParentId);
        dbContext.OrgUnits.Add(unit);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return unit.Id;
    }
}
