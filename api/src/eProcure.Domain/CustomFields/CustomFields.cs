using eProcure.Domain.Views;

namespace eProcure.Domain.CustomFields;

/// <summary>
/// The D5 launch set (Step 0(a), as ruled). RecordRef and DateTime are DEFERRED (BACKLOG):
/// no consumer exists — every user-entered business date is DateOnly (Slice H), and L4
/// custom records will be RecordRef's first honest consumer.
/// </summary>
// CF-FIX1-T8: the operator's full type set (NetSuite parity). New members REUSE the sparse
// columns (Percent→ValueNumber; Email/Telephone/Hyperlink/Image/Document→ValueText) except
// DateTime (new ValueDateTime). Display labels live client-side (Free-Form Text, …).
public enum CustomFieldDataType { Text, LongText, Int, Decimal, Money, Date, Bool, ListValue, DateTime, Percent, Email, Telephone, Hyperlink, Image, Document }

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
    public string Code { get; set; } = default!;          // custbody_* — UQ, immutable
    public string Label { get; set; } = default!;
    // CF-FIX2-T3: the def is authored ONCE and APPLIED to record types via
    // CustomFieldDefApplication rows (NetSuite's applies-to). The old single
    // RecordType column is gone — its data lives on as one application row each.
    public CustomFieldDataType DataType { get; set; }
    public Guid? CustomListId { get; set; }               // ListValue only — rides the conformed-dimension machinery
    public bool Required { get; set; }                    // enforced at VALUE-SAVE only; never gates record
                                                          // lifecycle transitions this slice (D7 form-engine territory)
    public string HelpText { get; set; } = "";
    public bool Active { get; set; } = true;
    public int Sort { get; set; }
    // CF4-T12 authoring parity: display type is DEF-level default rendering everywhere the
    // field appears (Normal = editable; Disabled = greyed input; Inline = plain text). Non-
    // Normal fields are NOT user-editable — the service enforces it, not just the UI.
    public string DisplayType { get; set; } = "Normal";  // Normal | Disabled | Inline
    // CF4-T12: surfaces the field as a column on the record type's SYSTEM view runs.
    public bool ShowInList { get; set; }
    // CF6-T1: Header = body field (today's grain); Line = transaction-line column field.
    // IMMUTABLE after creation (like Code/RecordType/DataType, same corruption reason).
    public string Scope { get; set; } = "Header";         // Header | Line
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

/// <summary>CF-FIX2-T3: one row per record type a def APPLIES to — the NetSuite
/// applies-to set. Values already carry their own RecordType per row, so a shared def
/// stores values under several types with no storage change. Grain: (def, type) UQ.</summary>
public class CustomFieldDefApplication
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid FieldDefId { get; set; }
    public RecordType RecordType { get; set; }
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
    // CF6-T1: null = header value (every pre-CF6 row, unchanged — no backfill); set = the
    // owning line. Uniqueness is TWO PARTIAL INDEXES (header: (def,record) WHERE LineId IS
    // NULL; line: (def,record,line) WHERE NOT NULL) — version-independent on PG13–16, the
    // locked ruling (NULLS NOT DISTINCT is PG15+ and prod's version is unverified).
    public Guid? LineId { get; set; }

    public string? ValueText { get; set; }                // Text/LongText + Email/Telephone/Hyperlink(url)/Image/Document(file ref)
    public decimal? ValueNumber { get; set; }             // Int + Decimal — numeric(18,4)
    public decimal? ValueMoney { get; set; }              // numeric(18,2), golden rule 3
    public DateOnly? ValueDate { get; set; }
    public bool? ValueBool { get; set; }
    public string? ValueListCode { get; set; }            // the CustomListValue CODE, like built-in selects
    public DateTime? ValueDateTime { get; set; }          // CF-FIX1-T8: Date/Time (timestamptz)
    /// <summary>CF-FIX1-T8: Hyperlink display label — a COMPANION to ValueText's url, NOT a
    /// value column (excluded from the ExactlyOne CHECK).</summary>
    public string? ValueLabel { get; set; }

    public DateTime UpdatedUtc { get; set; }
}
