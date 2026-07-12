namespace eProcure.Application.CustomFields;

/// <summary>Invalid custom-field definition or value → HTTP 400, the loud-validation posture.</summary>
public sealed class CustomFieldValidationException(string message) : Exception(message);

public sealed record CustomFieldDefDto(
    Guid Id, string Code, string Label, string RecordType, string DataType, Guid? CustomListId,
    bool Required, string HelpText, bool Active, int Sort, int ValueCount);

public sealed record SaveCustomFieldDefRequest(
    string Label, string RecordType, string DataType, Guid? CustomListId, bool Required, string HelpText, int Sort);

/// <summary>One field on one record, def metadata + the value as a STRING in the stored
/// formats the FieldSpec pipeline already uses (ISO dates, raw numerics, 'true'/'false',
/// list-value codes). Null value = no row = the honest null.</summary>
public sealed record CustomValueDto(
    string Code, string Label, string DataType, bool Required, string HelpText,
    string? CustomListCode, string? Value);

public sealed record SaveCustomValuesRequest(Dictionary<string, string?> Values);

public interface ICustomFieldService
{
    // Defs (the Admin Setup surface — A65)
    Task<IReadOnlyList<CustomFieldDefDto>> ListDefsAsync(string? recordType, CancellationToken ct = default);
    Task<CustomFieldDefDto> CreateDefAsync(SaveCustomFieldDefRequest req, CancellationToken ct = default);
    Task<CustomFieldDefDto> UpdateDefAsync(Guid id, SaveCustomFieldDefRequest req, CancellationToken ct = default);
    Task<CustomFieldDefDto> SetDefActiveAsync(Guid id, bool active, CancellationToken ct = default);
    Task DeleteDefAsync(Guid id, CancellationToken ct = default);   // zero-value only (ruled)

    // Values (A66/A67 — dynamic View* + the record type's scoped detail fetch)
    Task<IReadOnlyList<CustomValueDto>> GetValuesAsync(string recordType, Guid recordId, CancellationToken ct = default);
    Task<IReadOnlyList<CustomValueDto>> SaveValuesAsync(string recordType, Guid recordId, SaveCustomValuesRequest req, CancellationToken ct = default);
}
