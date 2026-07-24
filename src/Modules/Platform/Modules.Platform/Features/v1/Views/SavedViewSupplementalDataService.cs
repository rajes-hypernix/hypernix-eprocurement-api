using FSH.Modules.Platform.Contracts.Services;
using FSH.Modules.Platform.Data;
using FSH.Modules.Platform.Domain;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.Views;

/// <summary>
/// Phase 7 wiring: pulls header-grain <see cref="CustomFieldValue"/>/<see cref="SegmentAssignment"/>
/// rows for a batch of records and shapes them into the same <c>Custom_{Code}</c>/<c>Segment_{Dimension}</c>
/// field-key convention the registry seeds in <see cref="ViewsSeedData"/> — line-scoped custom
/// fields are deliberately excluded, since a saved view row is one row per header record, not per line.
/// </summary>
public sealed class SavedViewSupplementalDataService(PlatformDbContext dbContext) : ISavedViewSupplementalDataService
{
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, object?>>> GetSupplementalFieldsAsync(
        string recordType, IReadOnlyList<Guid> recordIds, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(recordIds);

        var result = new Dictionary<Guid, Dictionary<string, object?>>();
        if (recordIds.Count == 0 || !Enum.TryParse<PlatformRecordType>(recordType, ignoreCase: true, out var platformRecordType))
            return Freeze(result);

        var defs = await dbContext.CustomFieldDefs
            .AsNoTracking()
            .Include(d => d.Applications)
            .Where(d => d.IsActive && d.Scope == CustomFieldScope.Header && d.Applications.Any(a => a.RecordType == platformRecordType))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (defs.Count > 0)
        {
            var defIds = defs.Select(d => d.Id).ToList();
            var values = await dbContext.CustomFieldValues
                .AsNoTracking()
                .Where(v => v.RecordType == platformRecordType && v.LineId == null
                    && defIds.Contains(v.CustomFieldDefId) && recordIds.Contains(v.RecordId))
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var defsById = defs.ToDictionary(d => d.Id);
            foreach (var value in values)
            {
                if (!defsById.TryGetValue(value.CustomFieldDefId, out var def))
                    continue;

                GetBucket(result, value.RecordId)[$"Custom_{def.Code}"] = ExtractRawValue(value);
            }
        }

        var assignments = await dbContext.SegmentAssignments
            .AsNoTracking()
            .Where(a => a.RecordType == platformRecordType && a.LineId == null && recordIds.Contains(a.RecordId))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (assignments.Count > 0)
        {
            var orgUnitIds = assignments.Select(a => a.OrgUnitId).Distinct().ToList();
            var orgUnitNames = await dbContext.OrgUnits
                .AsNoTracking()
                .Where(o => orgUnitIds.Contains(o.Id))
                .ToDictionaryAsync(o => o.Id, o => o.Name, ct)
                .ConfigureAwait(false);

            foreach (var assignment in assignments)
            {
                GetBucket(result, assignment.RecordId)[ViewsSeedData.SegmentFieldKey(assignment.Dimension)] =
                    orgUnitNames.GetValueOrDefault(assignment.OrgUnitId);
            }
        }

        return Freeze(result);
    }

    private static Dictionary<string, object?> GetBucket(Dictionary<Guid, Dictionary<string, object?>> result, Guid recordId)
    {
        if (!result.TryGetValue(recordId, out var bucket))
        {
            bucket = [];
            result[recordId] = bucket;
        }

        return bucket;
    }

    private static IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, object?>> Freeze(Dictionary<Guid, Dictionary<string, object?>> result) =>
        result.ToDictionary(kv => kv.Key, kv => (IReadOnlyDictionary<string, object?>)kv.Value);

    private static object? ExtractRawValue(CustomFieldValue value) => value.DataType switch
    {
        CustomFieldDataType.Text or CustomFieldDataType.LongText or CustomFieldDataType.Email
            or CustomFieldDataType.Telephone or CustomFieldDataType.Hyperlink => value.ValueText,
        CustomFieldDataType.Int or CustomFieldDataType.Decimal or CustomFieldDataType.Money or CustomFieldDataType.Percent => value.ValueNumber,
        CustomFieldDataType.Date => value.ValueDate,
        CustomFieldDataType.DateTime => value.ValueDateTime,
        CustomFieldDataType.Bool => value.ValueBool,
        CustomFieldDataType.ListValue => value.ValueListCode,
        CustomFieldDataType.RecordRef => value.ValueLabel,
        _ => null,
    };
}
