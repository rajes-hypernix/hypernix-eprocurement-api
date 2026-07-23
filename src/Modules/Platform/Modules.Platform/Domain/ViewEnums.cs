namespace FSH.Modules.Platform.Domain;

/// <summary>The record types a saved view can query — one per list surface. Stored as
/// string (HasConversion) so adding a member later is additive, never a reshape.</summary>
public enum ViewRecordType
{
    Requisition,
    Rfq,
}

/// <summary>Native = seeded from the list DTOs/registry; Custom and Segment are reserved
/// for later slices that add rows without reshaping this table.</summary>
public enum ViewFieldKind
{
    Native,
    Custom,
    Segment,
}

/// <summary>Drives which primitive renders the criterion value input and how the filter
/// executor parses/compares values at run time.</summary>
public enum ViewFieldDataType
{
    Code,
    Text,
    Enum,
    Date,
    Instant,
    Money,
    Number,
    Bool,
    Tags,
}

/// <summary>The wave-1 operator set the in-memory filter executor understands.</summary>
public enum ViewOperator
{
    Eq,
    Neq,
    In,
    Contains,
    StartsWith,
    IsEmpty,
    IsNotEmpty,
    Gte,
    Lte,
    Between,
}

public enum ViewSortDirection
{
    Asc,
    Desc,
}
