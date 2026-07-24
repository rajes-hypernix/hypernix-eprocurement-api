using FSH.Framework.Core.Exceptions;
using FSH.Modules.Platform.Contracts.v1.CustomFields;
using FSH.Modules.Platform.Data;
using FSH.Modules.Platform.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.CustomFields.RemoveCustomFieldApplication;

public sealed class RemoveCustomFieldApplicationCommandHandler(PlatformDbContext dbContext)
    : ICommandHandler<RemoveCustomFieldApplicationCommand, Guid>
{
    public async ValueTask<Guid> Handle(RemoveCustomFieldApplicationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var def = await dbContext.CustomFieldDefs
            .Include(d => d.Applications)
            .FirstOrDefaultAsync(d => d.Id == command.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Custom field {command.Id} not found.");

        var recordType = CustomFieldMapping.ParseEnum<PlatformRecordType>(command.RecordType, "record type");
        def.RemoveApplication(recordType);

        // Mirror of the Apply-side sync — the field stops being filterable/columnable the moment the
        // application is removed. Existing saved views that already reference it keep the stale
        // FieldKey (harmless: it just never matches any row going forward), same as any other
        // dangling FieldKey — no cascade delete of SavedViewFilter/Column rows.
        if (Enum.TryParse<ViewRecordType>(recordType.ToString(), out var viewRecordType))
        {
            var fieldKey = $"Custom_{def.Code}";
            var fieldId = ViewsSeedData.StableFieldId(viewRecordType, fieldKey);
            var entry = await dbContext.FieldRegistryEntries
                .FirstOrDefaultAsync(f => f.Id == fieldId, cancellationToken)
                .ConfigureAwait(false);
            if (entry is not null)
                dbContext.FieldRegistryEntries.Remove(entry);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return def.Id;
    }
}
