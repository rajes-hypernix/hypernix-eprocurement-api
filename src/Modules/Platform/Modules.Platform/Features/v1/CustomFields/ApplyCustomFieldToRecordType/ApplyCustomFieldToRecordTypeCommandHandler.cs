using FSH.Framework.Core.Exceptions;
using FSH.Modules.Platform.Contracts.v1.CustomFields;
using FSH.Modules.Platform.Data;
using FSH.Modules.Platform.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.CustomFields.ApplyCustomFieldToRecordType;

public sealed class ApplyCustomFieldToRecordTypeCommandHandler(PlatformDbContext dbContext)
    : ICommandHandler<ApplyCustomFieldToRecordTypeCommand, Guid>
{
    public async ValueTask<Guid> Handle(ApplyCustomFieldToRecordTypeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var def = await dbContext.CustomFieldDefs
            .Include(d => d.Applications)
            .FirstOrDefaultAsync(d => d.Id == command.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Custom field {command.Id} not found.");

        var recordType = CustomFieldMapping.ParseEnum<PlatformRecordType>(command.RecordType, "record type");
        def.ApplyTo(recordType);

        // Phase 7: keep the Saved Views field registry in lockstep with Custom Fields — a header-scope
        // field becomes filterable/columnable the moment it's applied, with no separate admin step.
        if (def.Scope == CustomFieldScope.Header && Enum.TryParse<ViewRecordType>(recordType.ToString(), out var viewRecordType))
        {
            var fieldKey = $"Custom_{def.Code}";
            var fieldId = ViewsSeedData.StableFieldId(viewRecordType, fieldKey);
            bool alreadyRegistered = await dbContext.FieldRegistryEntries
                .AnyAsync(f => f.Id == fieldId, cancellationToken)
                .ConfigureAwait(false);
            if (!alreadyRegistered)
            {
                dbContext.FieldRegistryEntries.Add(FieldRegistryEntry.Create(
                    fieldId, viewRecordType, fieldKey, CustomFieldMapping.ToViewFieldDataType(def.DataType), def.Label, ViewFieldKind.Custom));
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return def.Id;
    }
}
