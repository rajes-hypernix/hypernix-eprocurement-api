using System.Security.Cryptography;
using System.Text;

namespace eProcure.Application.Segments;

/// <summary>
/// The four T6 system segments (ruled (iii-a)): the PR dimension columns remain the single
/// source of truth; these definitions + the migration backfill + the RequisitionService
/// projection expose them as reporting dimensions. Deterministic ids shared by the
/// migration, the projection, tests and the integrity probe. NO registry rows this slice
/// (ruled): the Native rows serve filtering; group-by addresses segments by definition;
/// the convergence BACKLOG row swaps Native→Segment in one move.
/// </summary>
public static class SegmentSeed
{
    public sealed record SystemSegment(string Code, string Name, string ColumnLabel, string ColumnCode);

    public static readonly IReadOnlyList<SystemSegment> SystemSegments =
    [
        new("seg_department", "Department", "Department", "DepartmentCode"),
        new("seg_location", "Location", "Location", "LocationCode"),
        new("seg_category", "Category", "Category", "CategoryCode"),
        new("seg_job", "Job", "Job", "JobCode"),
    ];

    // HEX-PARSE (not new Guid(byte[])): .NET's byte[] Guid ctor is mixed-endian, but the
    // migration's SQL uses md5(text)::uuid which is straight hex — Guid.Parse of the hex
    // string matches Postgres exactly, so C# and SQL derive IDENTICAL ids from the same input.
    private static Guid HexGuid(string input) =>
        Guid.Parse(Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(input))));

    public static Guid DefId(string code) => HexGuid($"segdef:{code}");

    /// <summary>Deterministic value id — the SAME formula as the migration's
    /// md5('segval:' || defId || ':' || valueCode)::uuid (defId lowercase-hyphenated).</summary>
    public static Guid ValueId(Guid defId, string valueCode) => HexGuid($"segval:{defId}:{valueCode}");
}
