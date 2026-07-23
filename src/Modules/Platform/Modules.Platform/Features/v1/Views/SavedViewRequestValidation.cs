using FSH.Modules.Platform.Contracts.Dtos;
using FSH.Modules.Platform.Data;
using FSH.Modules.Platform.Domain;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.Views;

/// <summary>
/// Validates a <see cref="SaveViewRequest"/> against the field registry — a dead key fails
/// loudly on save rather than silently dropping a filter/column at run time — and converts it
/// into the domain's input shape.
/// </summary>
internal static class SavedViewRequestValidation
{
    public static async Task<(ViewRecordType RecordType, IReadOnlyList<SavedViewFilterInput> Filters, IReadOnlyList<SavedViewColumnInput> Columns)>
        ValidateAndConvertAsync(PlatformDbContext dbContext, SaveViewRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(request);

        if (!Enum.TryParse<ViewRecordType>(request.RecordType, ignoreCase: true, out var recordType))
            throw new PlatformRuleException($"'{request.RecordType}' is not a recognised saved-view record type.");

        var validKeys = await dbContext.FieldRegistryEntries
            .AsNoTracking()
            .Where(f => f.RecordType == recordType)
            .Select(f => f.FieldKey)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var validKeySet = new HashSet<string>(validKeys, StringComparer.Ordinal);

        var filters = new List<SavedViewFilterInput>();
        foreach (var f in request.Filters ?? [])
        {
            RequireKnownField(validKeySet, f.FieldKey, recordType);
            if (!Enum.TryParse<ViewOperator>(f.Operator, ignoreCase: true, out var op))
                throw new PlatformRuleException($"'{f.Operator}' is not a recognised saved-view filter operator.");

            filters.Add(new SavedViewFilterInput(f.FieldKey, op, f.GroupIndex, f.Value, f.Value2, f.Sort));
        }

        var columns = new List<SavedViewColumnInput>();
        foreach (var c in request.Columns ?? [])
        {
            RequireKnownField(validKeySet, c.FieldKey, recordType);
            ViewSortDirection? sortDirection = null;
            if (!string.IsNullOrWhiteSpace(c.SortDirection))
            {
                if (!Enum.TryParse<ViewSortDirection>(c.SortDirection, ignoreCase: true, out var parsed))
                    throw new PlatformRuleException($"'{c.SortDirection}' is not a recognised sort direction.");
                sortDirection = parsed;
            }

            columns.Add(new SavedViewColumnInput(c.FieldKey, c.Label, c.Sort, sortDirection));
        }

        if (columns.Count == 0)
            throw new PlatformRuleException("A saved view needs at least one column.");

        return (recordType, filters, columns);
    }

    private static void RequireKnownField(HashSet<string> validKeys, string fieldKey, ViewRecordType recordType)
    {
        if (!validKeys.Contains(fieldKey))
            throw new PlatformRuleException($"'{fieldKey}' is not a registered field for record type '{recordType}'.");
    }
}
