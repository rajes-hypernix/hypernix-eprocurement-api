using eProcure.Domain.Views;

namespace eProcure.Domain.CustomFields;

/// <summary>
/// The D5 launch set (Step 0(a), as ruled). RecordRef and DateTime are DEFERRED (BACKLOG):
/// no consumer exists — every user-entered business date is DateOnly (Slice H), and L4
/// custom records will be RecordRef's first honest consumer.
/// </summary>
public enum CustomFieldDataType { Text, LongText, Int, Decimal, Money, Date, Bool, ListValue }

/// <summary>
/// A custom field definition — the D3 registry's Kind=Custom rows point here. Code
/// (cf_*) doubles as the registry FieldKey and the FieldSpec key, so a custom field
/// rides the IDENTICAL render/filter/aggregate pipeline as every built-in (charter
/// rule 3 — this slice is what the ruling was made for). Code, RecordType and DataType
/// are IMMUTABLE after creation (changing type under existing values would corrupt).
/// Lifecycle (ruled): values ever written → deactivate-only forever; zero values →
/// hard-delete allowed (typo cleanup, registry row removed). Grain: one row per def.
/// </summary>
public class CustomFieldDef
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Code { get; set; } = default!;          // cf_warranty_expiry — UQ, immutable
    public string Label { get; set; } = default!;
    public RecordType RecordType { get; set; }
    public CustomFieldDataType DataType { get; set; }
    public Guid? CustomListId { get; set; }               // ListValue only — rides the conformed-dimension machinery
    public bool Required { get; set; }                    // enforced at VALUE-SAVE only; never gates record
                                                          // lifecycle transitions this slice (D7 form-engine territory)
    public string HelpText { get; set; } = "";
    public bool Active { get; set; } = true;
    public int Sort { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

/// <summary>
/// One record's value for one def. SIX sparse typed columns serve the eight DataTypes
/// (ruled consolidation: Text+LongText share ValueText — length is UI semantics;
/// Int+Decimal share ValueNumber — wholeness is a save-time rule). Two DB CHECKs
/// preserve full type-safety: exactly one column populated, and the populated column
/// matches the denormalized DataType (kept in sync with the def, which is immutable).
/// No JSON values, ever — the typed core holds (charter rule 1).
/// Polymorphic (RecordType, RecordId) is the accepted cost: no owning aggregate
/// hard-deletes today, so the Postgres orphan integrity test is the standing guard.
/// Grain: one row per (def, record); an absent row IS the honest null.
/// </summary>
public class CustomFieldValue
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid FieldDefId { get; set; }
    public RecordType RecordType { get; set; }
    public Guid RecordId { get; set; }
    public CustomFieldDataType DataType { get; set; }     // denormalized from the (immutable) def for the CHECK

    public string? ValueText { get; set; }                // Text + LongText
    public decimal? ValueNumber { get; set; }             // Int + Decimal — numeric(18,4)
    public decimal? ValueMoney { get; set; }              // numeric(18,2), golden rule 3
    public DateOnly? ValueDate { get; set; }
    public bool? ValueBool { get; set; }
    public string? ValueListCode { get; set; }            // the CustomListValue CODE, like built-in selects

    public DateTime UpdatedUtc { get; set; }
}
