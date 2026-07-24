using FSH.Framework.Core.Exceptions;
using FSH.Modules.Platform.Contracts.v1.EntryForms;
using FSH.Modules.Platform.Data;
using FSH.Modules.Platform.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.EntryForms.UpsertEntryFormRoleMap;

public sealed class UpsertEntryFormRoleMapCommandHandler(PlatformDbContext dbContext)
    : ICommandHandler<UpsertEntryFormRoleMapCommand, Guid>
{
    public async ValueTask<Guid> Handle(UpsertEntryFormRoleMapCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var recordType = CustomFieldMapping.ParseEnum<PlatformRecordType>(command.RecordType, "record type");

        var form = await dbContext.EntryFormDefs
            .FirstOrDefaultAsync(f => f.Id == command.EntryFormDefId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Entry form {command.EntryFormDefId} not found.");

        if (form.RecordType != recordType)
        {
            throw new PlatformRuleException($"{form.Code} is a {form.RecordType} form, not {recordType}.");
        }

        var map = await dbContext.EntryFormRoleMaps
            .FirstOrDefaultAsync(m => m.RecordType == recordType && m.Role == command.Role, cancellationToken)
            .ConfigureAwait(false);

        if (map is null)
        {
            map = EntryFormRoleMap.Create(recordType, command.Role, command.EntryFormDefId);
            dbContext.EntryFormRoleMaps.Add(map);
        }
        else
        {
            map.Repoint(command.EntryFormDefId);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return map.Id;
    }
}
