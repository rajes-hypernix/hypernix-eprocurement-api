using eProcure.Domain.Forms;
using eProcure.Domain.Views;

namespace eProcure.Application.Forms;

/// <summary>Invalid form/numbering configuration, or an unmet requiredOnForm at submit
/// → HTTP 400, the loud posture.</summary>
public sealed class FormValidationException(string message) : Exception(message);

public sealed record EntryFormFieldDto(
    string FieldKey, string? Subtab, string FieldGroup, int Sort, string DisplayType,
    bool RequiredOnForm, string? DefaultValue, string? SourceFieldKey, bool FullWidth,
    string? Label, string? Placeholder);

// CF5: subtabs and groups as first-class objects on the def DTO (additive — the string
// placement on each field stays the composer's wire language; these carry the object
// state the strings cannot: Hidden, ColumnBreak, explicit empty containers, Sort).
public sealed record EntryFormSubtabDto(Guid Id, string Name, int Sort, bool Hidden);
public sealed record EntryFormGroupDto(Guid Id, Guid? SubtabId, string Title, int Sort, bool ColumnBreak);
public sealed record SaveSubtabRequest(string Name, int Sort, bool Hidden);
public sealed record SaveGroupRequest(string Title, Guid? SubtabId, int Sort, bool ColumnBreak);

public sealed record EntryFormDefDto(
    Guid Id, string Code, string Name, string RecordType, bool IsSystem, bool Active,
    IReadOnlyList<EntryFormFieldDto> Fields, IReadOnlyList<string> Roles,
    IReadOnlyList<EntryFormSubtabDto>? Subtabs = null, IReadOnlyList<EntryFormGroupDto>? Groups = null);

public sealed record SaveEntryFormRequest(string Name, string RecordType, List<EntryFormFieldDto> Fields);

public sealed record AssignRolesRequest(List<string> Roles);

/// <summary>One field of the caller's RESOLVED form, ready to render: registry metadata
/// folded in (DataType/Kind pick the D1 primitive; Options for segment selects;
/// CustomListCode + SourceFieldKey ride the existing dependent-select machinery) and
/// DefaultValue already token-resolved (dates arrive as yyyy-MM-dd, applied to NEW
/// records only).</summary>
public sealed record ResolvedFormFieldDto(
    string FieldKey, string Label, string DataType, string Kind, string? Subtab,
    string FieldGroup, int Sort, string DisplayType, bool RequiredOnForm,
    string? DefaultValue, string? SourceFieldKey, bool FullWidth, string? Placeholder,
    string? CustomListCode, IReadOnlyList<SegmentOptionDto>? Options,
    bool GroupColumnBreak = false);

public sealed record SegmentOptionDto(string Code, string Label);

public sealed record ResolvedFormDto(
    Guid FormId, string FormCode, string FormName, string RecordType,
    IReadOnlyList<ResolvedFormFieldDto> Fields);

public sealed record NumberingSchemeDto(string RecordType, string Prefix, bool YearSegment, int Digits, string NextPreview);

public sealed record SaveNumberingSchemeRequest(string Prefix, bool YearSegment, int Digits);

public interface IEntryFormService
{
    Task<IReadOnlyList<EntryFormDefDto>> ListAsync(string? recordType, CancellationToken ct = default);
    Task<EntryFormDefDto> CreateAsync(SaveEntryFormRequest req, CancellationToken ct = default);
    Task<EntryFormDefDto> UpdateAsync(Guid id, SaveEntryFormRequest req, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<EntryFormDefDto> SetActiveAsync(Guid id, bool active, CancellationToken ct = default);   // CF2-T6: the missing inactivate verb
    Task<EntryFormDefDto> AssignRolesAsync(Guid id, AssignRolesRequest req, CancellationToken ct = default);
    // CF5-T2/T3: layout-object CRUD (guards: delete refuses non-empty containers).
    Task<EntryFormDefDto> CreateSubtabAsync(Guid formId, SaveSubtabRequest req, CancellationToken ct = default);
    Task<EntryFormDefDto> UpdateSubtabAsync(Guid formId, Guid subtabId, SaveSubtabRequest req, CancellationToken ct = default);
    Task<EntryFormDefDto> DeleteSubtabAsync(Guid formId, Guid subtabId, CancellationToken ct = default);
    Task<EntryFormDefDto> CreateGroupAsync(Guid formId, SaveGroupRequest req, CancellationToken ct = default);
    Task<EntryFormDefDto> UpdateGroupAsync(Guid formId, Guid groupId, SaveGroupRequest req, CancellationToken ct = default);
    Task<EntryFormDefDto> DeleteGroupAsync(Guid formId, Guid groupId, CancellationToken ct = default);
    /// <summary>The caller's form for a record type (A71 + dynamic View*): fixed global
    /// role precedence, first held role with an Active mapped form wins, Standard fallback.</summary>
    Task<ResolvedFormDto> ResolveAsync(string recordType, CancellationToken ct = default);
}

/// <summary>The narrow seam RequisitionService uses at submit — server-side re-resolution
/// (OD-D7-2: a client-sent formCode could dodge its role form's required fields; the
/// server re-resolves from the caller instead, so the form is never client-chosen).</summary>
public interface IEntryFormSubmitGuard
{
    /// <summary>Throws FormValidationException when a requiredOnForm field of the CALLER's
    /// resolved form is empty on the record (native = DTO/entity value; custom = value row
    /// exists; segment = assignment exists). Gates SUBMIT only, never draft save (OD-D7-3).</summary>
    Task EnsureSubmittableAsync(RecordType type, Guid recordId, IReadOnlyDictionary<string, string?> nativeValues, CancellationToken ct = default);
}

public interface INumberingService
{
    Task<IReadOnlyList<NumberingSchemeDto>> ListAsync(CancellationToken ct = default);
    Task<NumberingSchemeDto> UpdateAsync(string recordType, SaveNumberingSchemeRequest req, CancellationToken ct = default);
}

/// <summary>Per-record-type FORM-CONTROLLABLE native keys, derived MECHANICALLY from the
/// write contract (OD-D7-5 companion): a native key may be placed on an entry form iff
/// the record's save DTO carries it — placing a key with no write path would be a lie.
/// Requisition = SavePrRequest's header surface. Other types join as their entry
/// surfaces migrate (each gate widens this map + lifts the RecordType restriction).</summary>
public static class EntryFormVocabulary
{
    public static readonly IReadOnlyDictionary<RecordType, IReadOnlySet<string>> ControllableNativeKeys =
        new Dictionary<RecordType, IReadOnlySet<string>>
        {
            // SavePrRequest: Requestor, Department, Location, Category, Job, Memo, RequiredDate
            [RecordType.Requisition] = new HashSet<string>
                { "Requestor", "Department", "Location", "Category", "Job", "Memo", "RequiredDate" },
        };

    /// <summary>Record types with a consuming entry surface THIS slice (OD-D7-5).</summary>
    public static readonly IReadOnlySet<RecordType> ConsumableRecordTypes =
        new HashSet<RecordType> { RecordType.Requisition };
}
