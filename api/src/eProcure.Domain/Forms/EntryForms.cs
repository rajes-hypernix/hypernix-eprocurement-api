using eProcure.Domain.Views;

namespace eProcure.Domain.Forms;

// D7 — the ENTRY-FORM engine. Distinct from Domain.Sourcing.FormTemplate (RFQ bid
// questionnaires): an EntryForm carries BEHAVIOUR for a record-entry surface — which
// registry fields, in what groups/subtabs, with what display type, requiredness,
// default and sourcing — resolved per ROLE at render and re-resolved server-side at
// submit (OD-D7-2: the caller can never pick its own form).

/// <summary>How a form field renders (the D1 reserved FieldSpec.displayType, activated).
/// "Inline" is NetSuite's name for ReadOnly — we keep the D1 names.</summary>
public enum EntryFormDisplayType { Normal = 0, Disabled = 1, ReadOnly = 2, Hidden = 3 }

/// <summary>One entry-form definition. RecordType is restricted to Requisition this
/// slice (OD-D7-5: a definition with no consuming surface is a dummy); each surface
/// migration widens it at its own gate. IsSystem marks the seeded Standard forms —
/// read-only (the segments precedent); the composer's "New form" copies Standard.
/// Grain: one row per form.</summary>
public class EntryFormDef
{
    public Guid Id { get; set; }                           // deterministic for seeds
    public string Code { get; set; } = default!;           // ef_* — UQ, derived from Name, immutable
    public string Name { get; set; } = default!;
    public RecordType RecordType { get; set; }
    public bool IsSystem { get; set; }
    public bool Active { get; set; } = true;
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

/// <summary>One placed field. FieldKey addresses the FieldRegistry (all three Kinds) —
/// service-validated for liveness on save AND at render (the D3/D5 loud-fail rule; NO
/// FK, which would silently convert D5's ruled zero-value hard-delete into blocked
/// deletes). Subtab null = the main body; subtabs are pure layout containers holding
/// FIELDS only (sublists keep their built-in homes — custom sublists are the L4
/// boundary). Label/Placeholder/FullWidth are census-derived render overrides
/// (OD-D7-4 + byte-identical parity: "Job / Cost ref" is not the registry's "Job").
/// Grain: one row per (form, field).</summary>
/// <summary>CF5: a subtab as a MANAGED OBJECT (was a string label on placed fields).
/// Creatable empty, reorderable, hideable. Hidden is layout-only: required fields on a
/// hidden subtab still gate submit (warn-but-allow, ruled D2). Grain: one row per
/// (form, name).</summary>
public class EntryFormSubtab
{
    public Guid Id { get; set; }                           // deterministic for seeds/backfill
    public Guid FormDefId { get; set; }
    public string Name { get; set; } = default!;
    public int Sort { get; set; }
    public bool Hidden { get; set; }
}

/// <summary>CF5: a field group as an object — the section container (body or subtab).
/// ColumnBreak starts a new column at this group (NetSuite's column break; "same as
/// previous" is its absence). Grain: one row per (form, subtab-or-body, title).</summary>
public class EntryFormGroup
{
    public Guid Id { get; set; }                           // deterministic for seeds/backfill
    public Guid FormDefId { get; set; }
    public Guid? SubtabId { get; set; }                    // null = the main body
    public string Title { get; set; } = default!;
    public int Sort { get; set; }
    public bool ColumnBreak { get; set; }
    // CF-FIX4-T1 (L3, the Header invariant): every form has exactly one IsHeader group, on
    // the BODY, that can never be deleted (and never renamed on system forms). It is the
    // guaranteed landing zone for the field-creation cascade — placement can never reach
    // an impossible state. Enforced in EntryFormService, not the UI.
    public bool IsHeader { get; set; }
}

public class EntryFormField
{
    public Guid Id { get; set; }
    public Guid FormDefId { get; set; }
    public string FieldKey { get; set; } = default!;
    // CF5: placement is by GROUP object; the subtab derives from the group's SubtabId —
    // one FK, no field↔group subtab-consistency invariant to police.
    public Guid GroupId { get; set; }
    public bool ColumnBreak { get; set; }                  // field-level break inside its group
    public int Sort { get; set; }
    public EntryFormDisplayType DisplayType { get; set; }
    public bool RequiredOnForm { get; set; }               // gates SUBMIT, never draft save (OD-D7-3)
    public string? DefaultValue { get; set; }              // static; date fields accept the ruled @-tokens
    public string? SourceFieldKey { get; set; }            // dependent sourcing: the sibling parentField (existing machinery)
    public bool FullWidth { get; set; }                    // memo-style row (census: PrForm Memo)
    public string? Label { get; set; }                     // null -> registry label
    public string? Placeholder { get; set; }
}

/// <summary>Which form a role gets for a record type (one per role per type). Resolution
/// (ruled, MODIFIED): a FIXED GLOBAL role precedence — a documented constant beside the
/// resolver, never the user record's array order — first role with an Active mapped form
/// wins; no hit falls back to the IsSystem Standard form.
/// Grain: one row per (record type, role).</summary>
public class EntryFormRoleMap
{
    public Guid Id { get; set; }
    public RecordType RecordType { get; set; }
    public string Role { get; set; } = default!;
    public Guid FormDefId { get; set; }
}

/// <summary>Numbering as admin configuration over the Slice G generator (D7, OD-D7-6):
/// one scheme per registry record type, consulted at FORMAT time only — the gap-free
/// FOR-UPDATE upsert is untouched. YearSegment=false buckets the sequence under year 0
/// (one continuous counter), so year-less codes cannot collide across years BY
/// CONSTRUCTION. Sequences are never reset: a prefix change starts/reattaches its own
/// counter, so history is never re-issued. System-artifact prefixes (GRN, BID, AWD, VU,
/// USR, FORM, DASH, VIEW) stay literal (BACKLOG row).
/// Grain: one row per record type.</summary>
public class NumberingScheme
{
    public Guid Id { get; set; }                           // deterministic for seeds
    public RecordType RecordType { get; set; }             // UQ
    public string Prefix { get; set; } = default!;         // A-Z / 0-9 / dash
    public bool YearSegment { get; set; } = true;
    public int Digits { get; set; } = 4;                   // 3..6
    public DateTime UpdatedUtc { get; set; }
}
