using eProcure.Domain.Views;

namespace eProcure.Domain.Segments;

/// <summary>
/// A user-defined REPORTING DIMENSION (D6): one definition, applied to multiple record
/// types (header or line level), every assignment a dimension key BY CONSTRUCTION (an FK
/// to a controlled value — never a derived string). IsSystem marks the four T6-migrated PR
/// dimensions (ruled (iii-a)): their columns remain the single source of truth and the
/// assignments are a one-way projection until the convergence row lands.
/// Grain: one row per definition.
/// </summary>
public class SegmentDef
{
    public Guid Id { get; set; }                           // deterministic for system seeds
    public string Code { get; set; } = default!;           // seg_project — UQ, immutable
    public string Name { get; set; } = default!;
    public bool HasHierarchy { get; set; }                 // flat-with-parent-stored (ruled (c)); rollup is BACKLOG
    public bool Required { get; set; }                     // value-save semantics only (the D5 rule carries over)
    public bool Active { get; set; } = true;
    public bool IsSystem { get; set; }                     // the T6 four — not deletable, convergence-owned
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

/// <summary>One controlled value. For the SYSTEM segments, Code identity with the PR's
/// *Code columns is guaranteed by construction: the backfill copies the columns (which
/// hold SourcingMapping.DimCode output) and the projection derives new values through the
/// SAME DimCode — the ruled condition that makes the column≡assignment probe meaningful.
/// Grain: one row per (segment, value).</summary>
public class SegmentValue
{
    public Guid Id { get; set; }                           // deterministic: md5("segval:{defId}:{code}")
    public Guid SegmentDefId { get; set; }
    public string Code { get; set; } = default!;
    public string Label { get; set; } = default!;
    public Guid? ParentValueId { get; set; }               // stored from day one; hierarchy UX/rollup gate-driven
    public bool Active { get; set; } = true;
    public int Sort { get; set; }
}

/// <summary>Which record types a segment applies to, and at which grain.
/// Grain: one row per (segment, record type).</summary>
public class SegmentApplication
{
    public Guid Id { get; set; }
    public Guid SegmentDefId { get; set; }
    public RecordType RecordType { get; set; }
    public bool LineLevel { get; set; }                    // rides Slice H's stable line Guids
}

/// <summary>One record's (or line's) dimension key. LineId null = header-level.
/// Grain: one row per (segment, record, line?); UQ enforced.</summary>
public class SegmentAssignment
{
    public Guid Id { get; set; }
    public Guid SegmentDefId { get; set; }
    public Guid SegmentValueId { get; set; }
    public RecordType RecordType { get; set; }
    public Guid RecordId { get; set; }
    public Guid? LineId { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
