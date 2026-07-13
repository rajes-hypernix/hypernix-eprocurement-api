namespace eProcure.Application.CustomFields;

/// <summary>Invalid custom-field definition or value → HTTP 400, the loud-validation posture.</summary>
public sealed class CustomFieldValidationException(string message) : Exception(message);

public sealed record CustomFieldDefDto(
    Guid Id, string Code, string Label, string RecordType, string DataType, Guid? CustomListId,
    bool Required, string HelpText, bool Active, int Sort, int ValueCount,
    string DisplayType = "Normal", bool ShowInList = false,
    string Scope = "Header",    // CF6-T1: Header | Line (immutable)
    IReadOnlyList<string>? RecordTypes = null);   // CF-FIX2-T3: the applies-to set (RecordType = first, wire-compat)

// CF-FIX1-T2: InsertBeforeId REMOVED (operator ruling — placement belongs to the form
// layout editor, CF5). The plain Sort integer remains the default order.
public sealed record SaveCustomFieldDefRequest(
    string Label, string RecordType, string DataType, Guid? CustomListId, bool Required, string HelpText, int Sort,
    string DisplayType = "Normal", bool ShowInList = false,
    string Scope = "Header",        // CF6-T1: Header | Line
    string? Code = null,            // CF-FIX1-T5: user-set Internal ID (namespace system-enforced); null → derived from label
    List<string>? RecordTypes = null);   // CF-FIX2-T3: applies-to set; null → [RecordType] (back-compat)

/// <summary>One field on one record, def metadata + the value as a STRING in the stored
/// formats the FieldSpec pipeline already uses (ISO dates, raw numerics, 'true'/'false',
/// list-value codes). Null value = no row = the honest null.</summary>
public sealed record CustomValueDto(
    string Code, string Label, string DataType, bool Required, string HelpText,
    string? CustomListCode, string? Value, string DisplayType = "Normal");

/// <summary>CF6-T1: Values = header grain (unchanged); Lines = per-line dictionaries keyed
/// by the owning line id — every line must belong to the record (server-verified).</summary>
public sealed record SaveCustomValuesRequest(
    Dictionary<string, string?> Values,
    Dictionary<Guid, Dictionary<string, string?>>? Lines = null);

/// <summary>CF-FIX3-T2: the impact report — every registered consumer's references plus
/// the data summary. What the admin sees before ANY destructive action.</summary>
public sealed record ImpactReportDto(
    IReadOnlyList<FieldReference> ConfigReferences,
    IReadOnlyList<DataReferenceSummary> Data,
    int LiveCount, int HistoricalCount,
    bool CanDelete, bool CanPurge, string? BlockedReason);

public interface ICustomFieldService
{
    // CF-FIX3: impact report + Tier-3 purge (T2/T3)
    Task<ImpactReportDto> GetReferencesAsync(Guid id, CancellationToken ct = default);
    Task PurgeAsync(Guid id, CancellationToken ct = default);
    // Defs (the Admin Setup surface — A65)
    Task<IReadOnlyList<CustomFieldDefDto>> ListDefsAsync(string? recordType, CancellationToken ct = default);
    Task<CustomFieldDefDto> CreateDefAsync(SaveCustomFieldDefRequest req, CancellationToken ct = default);
    Task<CustomFieldDefDto> UpdateDefAsync(Guid id, SaveCustomFieldDefRequest req, CancellationToken ct = default);
    Task<CustomFieldDefDto> SetDefActiveAsync(Guid id, bool active, CancellationToken ct = default);
    Task DeleteDefAsync(Guid id, CancellationToken ct = default);   // zero-value only (ruled)

    // Values (A66/A67 — dynamic View* + the record type's scoped detail fetch)
    Task<IReadOnlyList<CustomValueDto>> GetValuesAsync(string recordType, Guid recordId, CancellationToken ct = default);
    Task<IReadOnlyList<CustomValueDto>> SaveValuesAsync(string recordType, Guid recordId, SaveCustomValuesRequest req, CancellationToken ct = default);
    // CF6-T1: line-grain reads — lineId → the record's Line-scope values on that line.
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<CustomValueDto>>> GetLineValuesAsync(string recordType, Guid recordId, CancellationToken ct = default);
    // CF6-T3: the record type's LINE defs (no record, no values) — entry surfaces build columns from this.
    Task<IReadOnlyList<CustomValueDto>> GetLineDefsAsync(string recordType, CancellationToken ct = default);
}
