namespace FSH.Modules.Platform.Contracts.Services;

/// <summary>
/// Resolves the <c>Custom</c>/<c>Segment</c>-kind field values (see <c>ViewFieldKind</c>) that a
/// row source itself has no way to compute — those live in Platform's own
/// <c>CustomFieldValue</c>/<c>SegmentAssignment</c> tables, not the owning module's schema. Each
/// <c>ISavedViewRowSource</c> calls this once per batch of rows and merges the result into its own
/// PascalCase-keyed dictionaries before returning them to <c>RunSavedViewQueryHandler</c>.
/// </summary>
public interface ISavedViewSupplementalDataService
{
    /// <summary>
    /// Returns, per record id, the extra field-key/value pairs to merge into that record's row:
    /// one <c>Custom_{Code}</c> entry per active header-scope custom field applied to
    /// <paramref name="recordType"/>, and one <c>Segment_{Dimension}</c> entry per header-grain
    /// segment assignment on the record. A record id with nothing to contribute is omitted rather
    /// than returned with an empty dictionary.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, object?>>> GetSupplementalFieldsAsync(
        string recordType, IReadOnlyList<Guid> recordIds, CancellationToken ct);
}
