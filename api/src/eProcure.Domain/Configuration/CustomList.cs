namespace eProcure.Domain.Configuration;

/// <summary>
/// A reusable <b>Custom List</b> (NetSuite-style): a named, coded set of values that any field can be
/// tagged to. Fully data-driven — new lists and values are added as data, never hardcoded in a form
/// (DATA-MODEL-ANALYTICS §4, conformed dimensions). Grain: one list.
/// </summary>
public class CustomList
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    /// <summary>Stable key a field is tagged to, e.g. "COUNTRY", "STATE", "CITY", "BANK".</summary>
    public string Code { get; set; } = default!;

    /// <summary>Display name, e.g. "Country".</summary>
    public string Name { get; set; } = default!;

    public string? Description { get; set; }

    /// <summary>For a dependent/cascading list, the list this one hangs off — e.g. STATE → "COUNTRY",
    /// CITY → "STATE". Null for a flat list.</summary>
    public string? ParentListCode { get; set; }

    /// <summary>Seeded system list — protected from deletion in the admin UI.</summary>
    public bool IsSystem { get; set; }

    /// <summary>CF1-T2 (NetSuite order option): how option pickers order the values —
    /// "Entered" (the Sort integers, today's behaviour) or "Alphabetical" (by label).</summary>
    public string OrderMode { get; set; } = "Entered";

    /// <summary>CF1-T2: an inactive LIST is hidden from pickers wholesale (values keep
    /// resolving for display of stored codes — the A2F-T3 value discipline, list-level).</summary>
    public bool Active { get; set; } = true;

    /// <summary>The list's values (a separate entity so they insert/update cleanly and are admin-editable).</summary>
    public List<CustomListValue> Values { get; set; } = [];

    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

/// <summary>
/// One value within a <see cref="CustomList"/> (grain: one value). The <see cref="Code"/> is the
/// stored source of truth (what records carry, what analytics groups by); the <see cref="Label"/> is
/// shown to users. <see cref="ParentValueCode"/> links a dependent value to its parent value
/// (a State's Country, a City's State).
/// </summary>
public class CustomListValue
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid CustomListId { get; set; }
    public string Code { get; set; } = default!;
    public string Label { get; set; } = default!;
    public string? ParentValueCode { get; set; }
    public int Sort { get; set; }
    public bool Active { get; set; } = true;
}
