namespace FSH.Modules.Sourcing.Domain;

/// <summary>
/// One questionnaire item on an RFQ (technical/commercial question, terms, or instruction).
/// Ad-hoc per-RFQ — no shared/reusable Form Template library exists yet (Platform work).
/// </summary>
public sealed class FormItem
{
    /// <summary>"question" | "terms" | "instruction".</summary>
    public string Kind { get; private set; }

    /// <summary>"technical" | "commercial".</summary>
    public string Group { get; private set; }

    public string Section { get; private set; }
    public string Label { get; private set; }

    /// <summary>One of: short_text, long_text, number, money, percent, list, multi, yesno, date, attachment, table, group.</summary>
    public string Type { get; private set; }

    public bool Required { get; private set; }

    /// <summary>Type-specific config (options/columns/rows/group fields/unit) as JSON.</summary>
    public string? ConfigJson { get; private set; }

    public string? Help { get; private set; }
    public int Order { get; private set; }

    public FormItem(string kind, string group, string section, string label, string type, bool required, string? configJson, string? help, int order)
    {
        Kind = kind;
        Group = group;
        Section = section;
        Label = label;
        Type = type;
        Required = required;
        ConfigJson = configJson;
        Help = help;
        Order = order;
    }
}
