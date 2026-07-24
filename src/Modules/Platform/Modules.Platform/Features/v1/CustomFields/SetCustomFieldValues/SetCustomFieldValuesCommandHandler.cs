using FSH.Modules.Platform.Contracts.v1.CustomFields;
using FSH.Modules.Platform.Data;
using FSH.Modules.Platform.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.CustomFields.SetCustomFieldValues;

public sealed class SetCustomFieldValuesCommandHandler(PlatformDbContext dbContext)
    : ICommandHandler<SetCustomFieldValuesCommand>
{
    public async ValueTask<Unit> Handle(SetCustomFieldValuesCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Values.Count == 0)
        {
            return Unit.Value;
        }

        var recordType = CustomFieldMapping.ParseEnum<PlatformRecordType>(command.RecordType, "record type");
        var defIds = command.Values.Select(v => v.CustomFieldDefId).Distinct().ToList();

        var defs = await dbContext.CustomFieldDefs
            .Include(d => d.Applications)
            .Where(d => defIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, cancellationToken)
            .ConfigureAwait(false);

        var existing = await dbContext.CustomFieldValues
            .Where(v => v.RecordType == recordType && v.RecordId == command.RecordId && defIds.Contains(v.CustomFieldDefId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var input in command.Values)
        {
            if (!defs.TryGetValue(input.CustomFieldDefId, out var def))
            {
                throw new PlatformRuleException($"Custom field {input.CustomFieldDefId} not found.");
            }

            if (!def.Applications.Any(a => a.RecordType == recordType))
            {
                throw new PlatformRuleException($"{def.Code} does not apply to {recordType}.");
            }

            var value = existing.FirstOrDefault(v => v.CustomFieldDefId == def.Id && v.LineId == input.LineId);
            if (value is null)
            {
                value = CustomFieldValue.Create(def.Id, def.DataType, recordType, command.RecordId, input.LineId);
                dbContext.CustomFieldValues.Add(value);
            }

            value.SetValue(
                input.ValueText, input.ValueNumber, input.ValueDate, input.ValueDateTime,
                input.ValueBool, input.ValueListCode, input.ValueRefId, input.ValueLabel);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}
