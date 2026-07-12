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
}
