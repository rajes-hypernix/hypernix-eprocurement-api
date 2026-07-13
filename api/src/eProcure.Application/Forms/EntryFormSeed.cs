using System.Security.Cryptography;
using System.Text;
using eProcure.Domain.Views;

namespace eProcure.Application.Forms;

/// <summary>
/// THE single source for D7's seeds (the FieldRegistrySeed/DashboardSeed/SegmentSeed
/// discipline): the migration loop, test seeding and the parity probe all read this.
/// The Standard PR Form reproduces PrForm's hardcoded HEADER_SECTION EXACTLY — same
/// keys, same labels, same placeholders, same 3-across rows, Memo full-width — so the
/// resolver-driven render is byte-identical to the markup it replaces (the P1 gate).
/// Numbering rows reproduce today's CodeGenerator formats verbatim.
/// </summary>
public static class EntryFormSeed
{
    public sealed record FieldRow(
        string FieldKey, string FieldGroup, int Sort, string? Label, string? Placeholder,
        bool FullWidth = false, string? Subtab = null);

    public sealed record SchemeRow(RecordType RecordType, string Prefix, bool YearSegment, int Digits);

    public const string StandardPrFormCode = "ef_standard_pr_form";
    public const string StandardPrFormName = "Standard PR Form";

    /// <summary>PrForm HEADER_SECTION, verbatim (rows of 3 by Sort; Memo full-width).</summary>
    public static readonly IReadOnlyList<FieldRow> StandardPrFields =
    [
        new("Requestor", "Header", 0, null, "Name"),
        new("Department", "Header", 1, null, "e.g. Maintenance"),
        new("Category", "Header", 2, null, "e.g. Piping"),
        new("Location", "Header", 3, null, "e.g. Bintulu Plant"),
        new("Job", "Header", 4, "Job / Cost ref", "JOB-…"),
        new("RequiredDate", "Header", 5, "Required by", null),
        new("Memo", "Header", 6, "Memo / Justification", "Short description of the requirement", FullWidth: true),
    ];

    /// <summary>Today's formats, verbatim: {PREFIX}-{YEAR}-{0:D4} for all seven.</summary>
    public static readonly IReadOnlyList<SchemeRow> Schemes =
    [
        new(RecordType.Requisition, "PR", true, 4),
        new(RecordType.Rfq, "RFQ", true, 4),
        new(RecordType.PurchaseOrder, "PO", true, 4),
        new(RecordType.Invoice, "INV", true, 4),
        new(RecordType.Asn, "ASN", true, 4),
        new(RecordType.Vendor, "SWK-V", true, 4),
        new(RecordType.Onboarding, "VOB", true, 4),
    ];

    // HEX-PARSE (not new Guid(byte[])) — the SegmentSeed rule: matches Postgres
    // md5(text)::uuid exactly, so C# and SQL derive IDENTICAL ids from the same input.
    private static Guid HexGuid(string input) =>
        Guid.Parse(Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(input))));

    public static Guid FormId(string code) => HexGuid($"entryform:{code}");
    public static Guid FieldId(Guid formId, string fieldKey) => HexGuid($"entryformfield:{formId}:{fieldKey}");
    public static Guid SchemeId(RecordType type) => HexGuid($"numberingscheme:{type}");
    // CF5 layout objects — the SAME md5 derivations the EntryFormLayout migration uses in SQL,
    // so C# seeds and the backfill mint IDENTICAL ids (the SegmentSeed rule).
    public static Guid SubtabId(Guid formId, string name) => HexGuid($"entryformsubtab:{formId}:{name}");
    public static Guid GroupId(Guid formId, string? subtabName, string title) => HexGuid($"entryformgroup:{formId}:{subtabName ?? ""}:{title}");

    /// <summary>Entity builders for TEST stores (the FieldRegistrySeed.ToEntities precedent):
    /// production gets these rows from the migration; in-memory test contexts seed the same
    /// invariant so the scheme-consulting mint and the submit guard behave identically.</summary>
    public static (Domain.Forms.EntryFormDef Def, List<Domain.Forms.EntryFormSubtab> Subtabs,
        List<Domain.Forms.EntryFormGroup> Groups, List<Domain.Forms.EntryFormField> Fields) ToStandardPrFormEntities()
    {
        var seeded = new DateTime(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc);
        var formId = FormId(StandardPrFormCode);
        var def = new Domain.Forms.EntryFormDef
        {
            Id = formId, Code = StandardPrFormCode, Name = StandardPrFormName,
            RecordType = RecordType.Requisition, IsSystem = true, Active = true,
            CreatedUtc = seeded, UpdatedUtc = seeded,
        };
        // CF5: layout objects, derived from the SAME FieldRow vocabulary (order of first appearance).
        var subtabs = StandardPrFields.Where(f => f.Subtab is not null).Select(f => f.Subtab!).Distinct()
            .Select((name, i) => new Domain.Forms.EntryFormSubtab
            {
                Id = SubtabId(formId, name), FormDefId = formId, Name = name, Sort = i, Hidden = false,
            }).ToList();
        var groups = StandardPrFields.Select(f => (f.Subtab, f.FieldGroup)).Distinct()
            .Select((g, i) => new Domain.Forms.EntryFormGroup
            {
                Id = GroupId(formId, g.Subtab, g.FieldGroup), FormDefId = formId,
                SubtabId = g.Subtab is null ? null : SubtabId(formId, g.Subtab),
                Title = g.FieldGroup, Sort = i, ColumnBreak = false,
                IsHeader = g.Subtab is null && g.FieldGroup == "Header",   // L3: the invariant group
            }).ToList();
        var fields = StandardPrFields.Select(f => new Domain.Forms.EntryFormField
        {
            Id = FieldId(formId, f.FieldKey), FormDefId = formId, FieldKey = f.FieldKey,
            GroupId = GroupId(formId, f.Subtab, f.FieldGroup), Sort = f.Sort,
            DisplayType = Domain.Forms.EntryFormDisplayType.Normal, RequiredOnForm = false,
            FullWidth = f.FullWidth, Label = f.Label, Placeholder = f.Placeholder,
        }).ToList();
        return (def, subtabs, groups, fields);
    }

    public static List<Domain.Forms.NumberingScheme> ToSchemeEntities()
    {
        var seeded = new DateTime(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc);
        return Schemes.Select(s => new Domain.Forms.NumberingScheme
        {
            Id = SchemeId(s.RecordType), RecordType = s.RecordType, Prefix = s.Prefix,
            YearSegment = s.YearSegment, Digits = s.Digits, UpdatedUtc = seeded,
        }).ToList();
    }
}
