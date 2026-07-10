namespace eProcure.Domain.Sourcing;

/// <summary>
/// One element of an RFQ question form: a scored question (one of 12 field types),
/// or a terms/instruction block. <see cref="ConfigJson"/> holds type-specific config
/// (options, columns/rows, group fields, unit) as JSON, mirroring the prototype.
/// Reused (as an owned type) by both <see cref="Rfq"/> and <see cref="FormTemplate"/>.
/// </summary>
public class FormItem
{
    public string Kind { get; set; } = "question";      // question | terms | instruction
    public string Group { get; set; } = "technical";    // technical | commercial
    public string Section { get; set; } = "";
    public string Label { get; set; } = "";
    public string Type { get; set; } = "short_text";    // one of FormItemVocab.Types
    public bool Required { get; set; }
    public string ConfigJson { get; set; } = "{}";
    public string Help { get; set; } = "";
    public int Order { get; set; }
}
