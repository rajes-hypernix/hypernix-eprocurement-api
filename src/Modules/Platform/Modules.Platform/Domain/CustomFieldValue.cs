using FSH.Framework.Core.Domain;

namespace FSH.Modules.Platform.Domain;

/// <summary>
/// A custom field's value against one record instance. Polymorphic (<see cref="RecordType"/>,
/// <see cref="RecordId"/>) pair, deliberately with no foreign key to any owning aggregate — no
/// record type here hard-deletes, and a value must survive its record's own lifecycle changes.
/// <see cref="LineId"/> is null for a header-scoped value, set for a line-scoped one.
///
/// Sparse-column storage, not JSON, not a single generic string column — <see cref="DataType"/> is
/// denormalized from the owning <see cref="CustomFieldDef"/> at creation time (the def's own
/// DataType is immutable, so this can never drift) and determines which single Value* column is
/// ever populated. This mirrors the old source's charter rule verbatim: "no JSON values, ever — the
/// typed core holds."
/// </summary>
public sealed class CustomFieldValue : AggregateRoot<Guid>
{
    public Guid CustomFieldDefId { get; private set; }
    public PlatformRecordType RecordType { get; private set; }
    public Guid RecordId { get; private set; }
    public Guid? LineId { get; private set; }
    public CustomFieldDataType DataType { get; private set; }

    public string? ValueText { get; private set; }
    public decimal? ValueNumber { get; private set; }
    public DateOnly? ValueDate { get; private set; }
    public DateTime? ValueDateTime { get; private set; }
    public bool? ValueBool { get; private set; }
    public string? ValueListCode { get; private set; }
    public Guid? ValueRefId { get; private set; }

    /// <summary>Denormalized display text for a RecordRef value — survives the target's own deletion/cancellation; a reference is provenance, not a constraint.</summary>
    public string? ValueLabel { get; private set; }

    public DateTime UpdatedUtc { get; private set; }

    private CustomFieldValue() { }

    public static CustomFieldValue Create(Guid customFieldDefId, CustomFieldDataType dataType, PlatformRecordType recordType, Guid recordId, Guid? lineId)
    {
        if (recordId == Guid.Empty)
        {
            throw new PlatformRuleException("A custom field value requires a record id.");
        }

        return new CustomFieldValue
        {
            Id = Guid.CreateVersion7(),
            CustomFieldDefId = customFieldDefId,
            DataType = dataType,
            RecordType = recordType,
            RecordId = recordId,
            LineId = lineId,
            UpdatedUtc = DateTime.UtcNow,
        };
    }

    /// <summary>Clears every Value* column, then sets only the one matching <see cref="DataType"/>.</summary>
    public void SetValue(
        string? text = null, decimal? number = null, DateOnly? date = null, DateTime? dateTime = null,
        bool? boolean = null, string? listCode = null, Guid? refId = null, string? refLabel = null)
    {
        ValueText = null;
        ValueNumber = null;
        ValueDate = null;
        ValueDateTime = null;
        ValueBool = null;
        ValueListCode = null;
        ValueRefId = null;
        ValueLabel = null;

        switch (DataType)
        {
            case CustomFieldDataType.Text or CustomFieldDataType.LongText or CustomFieldDataType.Email
                or CustomFieldDataType.Telephone or CustomFieldDataType.Hyperlink:
                ValueText = text;
                break;
            case CustomFieldDataType.Int or CustomFieldDataType.Decimal or CustomFieldDataType.Money or CustomFieldDataType.Percent:
                ValueNumber = number;
                break;
            case CustomFieldDataType.Date:
                ValueDate = date;
                break;
            case CustomFieldDataType.DateTime:
                ValueDateTime = dateTime;
                break;
            case CustomFieldDataType.Bool:
                ValueBool = boolean;
                break;
            case CustomFieldDataType.ListValue:
                ValueListCode = listCode;
                break;
            case CustomFieldDataType.RecordRef:
                ValueRefId = refId;
                ValueLabel = refLabel;
                break;
        }

        UpdatedUtc = DateTime.UtcNow;
    }
}
