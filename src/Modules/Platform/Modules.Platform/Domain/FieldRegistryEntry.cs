using FSH.Framework.Core.Domain;

namespace FSH.Modules.Platform.Domain;

/// <summary>
/// One queryable field per (RecordType, FieldKey) — the registry a view's filters and
/// columns are validated against on save and on run (an unknown key fails loudly, never
/// silently drops a filter). Grain: one row per field per record type.
/// </summary>
public sealed class FieldRegistryEntry : AggregateRoot<Guid>, IAuditableEntity
{
    public ViewRecordType RecordType { get; private set; }
    public string FieldKey { get; private set; } = default!;
    public ViewFieldKind Kind { get; private set; } = ViewFieldKind.Native;
    public string Label { get; private set; } = default!;
    public ViewFieldDataType DataType { get; private set; }

    public DateTimeOffset CreatedOnUtc { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? LastModifiedOnUtc { get; private set; }
    public string? LastModifiedBy { get; private set; }

    private FieldRegistryEntry() { }

    /// <summary>Native seeds pass a deterministic id so re-seeding is idempotent.</summary>
    public static FieldRegistryEntry Create(
        Guid id,
        ViewRecordType recordType,
        string fieldKey,
        ViewFieldDataType dataType,
        string label,
        ViewFieldKind kind = ViewFieldKind.Native,
        string? createdBy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        return new FieldRegistryEntry
        {
            Id = id,
            RecordType = recordType,
            FieldKey = fieldKey.Trim(),
            Kind = kind,
            Label = label.Trim(),
            DataType = dataType,
            CreatedOnUtc = TimeProvider.System.GetUtcNow(),
            CreatedBy = createdBy,
            LastModifiedOnUtc = null,
            LastModifiedBy = null,
        };
    }
}
