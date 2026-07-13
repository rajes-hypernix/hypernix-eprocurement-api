namespace eProcure.Domain.Views;

/// <summary>The record types a saved view can query — one per list surface (D3).</summary>
// CF-FIX4-T2: Grn appended — first-class record type for entry forms + custom-field
// applicability. Stored as STRING (HasConversion) and unmapped in RecordLifecycle,
// so it is LIVE/fail-closed by construction (the CF-FIX-3 pin covers it).
public enum RecordType { Requisition, Rfq, PurchaseOrder, Invoice, Asn, Vendor, Onboarding, Grn }

/// <summary>Native = seeded from the list DTOs; Custom (D5) and Segment (D6) are RESERVED —
/// later slices add rows, never reshape (AUTHORIZATION-MATRIX-era framework rule).</summary>
public enum FieldKind { Native, Custom, Segment }

/// <summary>Drives which D1 primitive renders the criterion value input and how the
/// executor parses/compares values (D3 Step 0(b), ruled).</summary>
public enum FieldDataType { Code, Text, Enum, Date, Instant, Money, Number, Bool, Tags }

/// <summary>The ruled operator set (D3 Step 0(a)) — each member cites a real client filter
/// it absorbs; Gte/Lte kept per ruling as Between's decomposition + the D4 "overdue" consumer.
/// Deliberately absent (deny-by-default): Neq, Gt/Lt strict, IsEmpty — no real filter needs
/// them; adding one later is an enum member + an executor case.</summary>
// CF7-T1 additions (Neq..NotContains). Null semantics are LOCKED and pinned by tests:
// Neq/NotIn/NotContains EXCLUDE null rows (null is unknown, not different); IsEmpty is
// the explicit ask for nulls. Stored by NAME (HasConversion<string>) — order-safe.
public enum ViewOperator { Eq, In, Contains, Between, Gte, Lte, Neq, Gt, Lt, StartsWith, IsEmpty, IsNotEmpty, NotIn, NotContains }

public enum ViewSortDirection { Asc, Desc }

/// <summary>
/// One queryable field per (RecordType, FieldKey) — the registry a view's filters and
/// columns are validated against on save AND on run (a dead key fails loudly, never
/// silently drops a filter). Grain: one row per field per record type.
/// </summary>
public class FieldRegistryEntry
{
    public Guid Id { get; set; }                       // deterministic for Native seeds (FieldRegistrySeed.StableId)
    public RecordType RecordType { get; set; }
    public string FieldKey { get; set; } = default!;   // DTO property name, PascalCase (DATA-MODEL naming)
    public FieldKind Kind { get; set; } = FieldKind.Native;
    public string Label { get; set; } = default!;
    public FieldDataType DataType { get; set; }

    // Definition FKs for the reserved kinds — nullable until D5/D6 add those tables.
    public Guid? CustomFieldDefId { get; set; }
    public Guid? SegmentDefId { get; set; }
}

/// <summary>
/// A saved query definition: filters + columns over one record type. The shared heart —
/// list screens now, D4 portlets/reminders/KPIs later. No filter blobs: every criterion
/// is a typed row. Grain: one row per view.
/// </summary>
public class SavedView
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Code { get; set; } = default!;       // VIEW-2026-0001 (server sequence); literal for system seeds
    public string Name { get; set; } = default!;
    public RecordType RecordType { get; set; }
    /// <summary>Null for system views; otherwise the creating principal (internal user id or vendor code).</summary>
    public string? OwnerUserId { get; set; }
    public bool IsShared { get; set; }
    public bool IsSystem { get; set; }
    public List<SavedViewFilter> Filters { get; set; } = [];
    public List<SavedViewColumn> Columns { get; set; } = [];
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

/// <summary>
/// One typed criterion row. Composition rule (mirrors the facet semantics the operators
/// were derived from): rows sharing a FieldKey with Eq/In OR together (membership);
/// everything else ANDs. Value2 only for Between. Values may be the ruled relative-date
/// tokens (@today, @startOfMonth, @endOfMonth), resolved by the executor at run time.
/// Grain: one row per criterion member.
/// </summary>
public class SavedViewFilter
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid SavedViewId { get; set; }
    public string FieldKey { get; set; } = default!;
    public ViewOperator Operator { get; set; }
    // CF7-T2 (locked: one-level grouped-OR): filters sharing a GroupIndex >= 1 OR together;
    // groups AND each other; 0 = ungrouped (today's composition, unchanged).
    public int GroupIndex { get; set; }
    public string Value { get; set; } = default!;
    public string? Value2 { get; set; }
    public int Sort { get; set; }
}

/// <summary>One output column of the view. Grain: one row per column position.</summary>
public class SavedViewColumn
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid SavedViewId { get; set; }
    public string FieldKey { get; set; } = default!;
    public string? Label { get; set; }                 // null = registry label
    public int Sort { get; set; }
    public ViewSortDirection? SortDirection { get; set; }
}
