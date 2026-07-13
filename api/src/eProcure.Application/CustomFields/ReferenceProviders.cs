namespace eProcure.Application.CustomFields;

/// <summary>
/// CF-FIX3 — THE ANTI-ROT FOUNDATION. Custom-field/list-value lifecycle decisions never
/// name a consumer: every consumer registers ONE of these providers in DI, and the guard,
/// the impact report and the purge LOOP over the injected collections. To add a new
/// consumer (a new analytics function, a new record type, a new value store): implement
/// the interface, add one DI line — NEVER edit the lifecycle code. This contract is
/// pinned by ExtensibilityProofTests (T6): a provider registered only in the test project
/// must appear in the report and block deletion with zero changes here or in the guard.
///
/// Config references and data references are DIFFERENT and the tiers treat them oppositely:
///   • config refs (form placements, view columns/filters, …) must reach ZERO before Delete
///     — the impact report is the admin's to-do list;
///   • data refs split Live vs Historical by each record type's TERMINAL status
///     (RecordLifecycle): Live blocks Delete and is NEVER purgeable; Historical is
///     removable only by the governed, snapshotted Tier-3 purge.
/// </summary>
public enum FieldRefKind { FormPlacement, ViewColumn, ViewFilter, SegmentDim, Other }

/// <summary>One structural reference held by a consumer. PER-INSTANCE grain, always —
/// e.g. the entry-form provider returns one row PER FORM (TargetId = the form id), so the
/// later per-form-applicability slice is just more rows, never a registry retrofit.</summary>
public sealed record FieldReference(
    string ConsumerName, FieldRefKind Kind, Guid? TargetId, string TargetLabel,
    string? Detail = null, bool IsMandatory = false);

public sealed record DataTypeCount(string RecordType, int Live, int Historical);

/// <summary>Stored-value counts split by record lifecycle. FAIL CLOSED: a record whose
/// status a provider cannot map counts as LIVE — unknown status is never purgeable.</summary>
public sealed record DataReferenceSummary(
    string StoreName, int LiveCount, int HistoricalCount, IReadOnlyList<DataTypeCount> ByRecordType);

public sealed record PurgedValue(string RecordType, Guid RecordId, Guid? LineId, string RecordLabel, string? Value);
public sealed record PurgeSnapshot(string StoreName, IReadOnlyList<PurgedValue> Removed);

/// <summary>A consumer holding STRUCTURAL references to a custom field (form placement,
/// view column/filter, segment dimension, a future analytics binding…).</summary>
public interface ICustomFieldReferenceProvider
{
    string ConsumerName { get; }
    Task<IReadOnlyList<FieldReference>> FindReferencesAsync(Guid fieldDefId, string fieldCode, CancellationToken ct = default);
}

/// <summary>A store holding VALUES for a custom field on actual records.</summary>
public interface ICustomFieldDataProvider
{
    string StoreName { get; }
    Task<DataReferenceSummary> CountValuesAsync(Guid fieldDefId, CancellationToken ct = default);
    /// <summary>Tier-3 only: remove values on HISTORICAL records, return them for the audit
    /// snapshot. Must never touch a Live (or unknown-status) record's value.</summary>
    Task<PurgeSnapshot> PurgeHistoricalAsync(Guid fieldDefId, CancellationToken ct = default);
}

/// <summary>Same pattern for LIST VALUES: a value can be a view filter-criterion (config)…</summary>
public interface ICustomListValueReferenceProvider
{
    string ConsumerName { get; }
    Task<IReadOnlyList<FieldReference>> FindReferencesAsync(Guid listId, string listCode, string valueCode, CancellationToken ct = default);
}

/// <summary>…and can be HELD by records (custom-field values storing the code, native
/// code-holding master-data columns). Master data is never historical — such usage is
/// always Live and blocks deletion.</summary>
public interface ICustomListValueDataProvider
{
    string StoreName { get; }
    Task<DataReferenceSummary> CountValueUsageAsync(Guid listId, string listCode, string valueCode, CancellationToken ct = default);
    Task<PurgeSnapshot> PurgeHistoricalAsync(Guid listId, string listCode, string valueCode, CancellationToken ct = default);
}

/// <summary>CF-FIX4-T6: the SEGMENT twins of the CF-FIX-3 registries. valueId null = the
/// def grain; non-null = one value. Same anti-rot contract: the guard/report LOOP over
/// registered providers — a new segment consumer is one interface + one DI line.</summary>
public interface ISegmentReferenceProvider
{
    string ConsumerName { get; }
    Task<IReadOnlyList<FieldReference>> FindReferencesAsync(Guid segmentDefId, string segmentCode, Guid? valueId, CancellationToken ct = default);
}

public interface ISegmentDataProvider
{
    string StoreName { get; }
    Task<DataReferenceSummary> CountAssignmentsAsync(Guid segmentDefId, Guid? valueId, CancellationToken ct = default);
    Task<PurgeSnapshot> PurgeHistoricalAsync(Guid segmentDefId, Guid? valueId, CancellationToken ct = default);
}

/// <summary>CF-FIX4-T8 — THE central archived-hiding predicate (anti-rot: ONE filter, every
/// value-reading surface applies it; a future surface reading through the standard paths
/// inherits the hiding with no extra work — the same discipline as the provider registries).
/// Admin/manage surfaces deliberately do NOT use it: archived defs must stay visible there,
/// or they could never be un-archived.</summary>
public static class CustomFieldVisibility
{
    /// <summary>True when the def's VALUES may appear on live surfaces.</summary>
    public static bool ValueVisible(Domain.CustomFields.CustomFieldDef d) => d.ArchivedUtc == null;

    /// <summary>The EF-translatable form of the same predicate.</summary>
    public static readonly System.Linq.Expressions.Expression<Func<Domain.CustomFields.CustomFieldDef, bool>> ValueVisibleExpr =
        d => d.ArchivedUtc == null;
}
