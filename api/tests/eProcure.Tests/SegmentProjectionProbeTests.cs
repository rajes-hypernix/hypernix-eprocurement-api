using eProcure.Application.Segments;
using eProcure.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace eProcure.Tests;

/// <summary>
/// The (iii-a) column≡assignment integrity probe — POSTGRES-BACKED per the ruling: a future
/// PR write path that forgets the projection hook leaves a PR whose column code disagrees
/// with (or lacks) its assignment, and THIS goes red against real data. This is what makes
/// the design a one-way projection and not dual-write drift: columns are the single truth,
/// and divergence is machine-caught, not hoped against.
/// </summary>
public sealed class SegmentProjectionProbeTests
{
    private static string Conn =>
        Environment.GetEnvironmentVariable("ConnectionStrings__Default")
        ?? "Host=localhost;Port=5432;Database=eprocure;Username=eprocure;Password=localdev";

    [Fact]
    public async Task Every_pr_dimension_column_agrees_with_its_system_segment_assignment()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(Conn).Options);
        if (!await db.Database.CanConnectAsync())
            throw new InvalidOperationException("Probe requires the local Docker Postgres (docker compose up -d).");

        var mismatches = new List<string>();
        foreach (var seg in SegmentSeed.SystemSegments)
        {
            var defId = SegmentSeed.DefId(seg.Code);
            // One row per PR: the column's code vs the assignment's value code (null when absent).
            // A2F-T7 (EF1002): defId rides as a REAL parameter; the column name cannot be a
            // parameter (identifier), but it comes from the compile-time SystemSegments list,
            // never input — the composed string carries no untrusted data.
            var sql = $$"""
                 SELECT p."Code" AS "PrCode", p."{{seg.ColumnCode}}" AS "ColumnCode", v."Code" AS "AssignedCode"
                 FROM "PurchaseRequisitions" p
                 LEFT JOIN "SegmentAssignments" a
                   ON a."SegmentDefId" = {0} AND a."RecordType" = 'Requisition' AND a."RecordId" = p."Id" AND a."LineId" IS NULL
                 LEFT JOIN "SegmentValues" v ON v."Id" = a."SegmentValueId"
                 """;
            var rows = await db.Database.SqlQueryRaw<ProbeRow>(sql, defId).ToListAsync();
            foreach (var r in rows)
            {
                var expected = string.IsNullOrEmpty(r.ColumnCode) ? null : r.ColumnCode;
                if (!string.Equals(expected, r.AssignedCode, StringComparison.Ordinal))
                    mismatches.Add($"{r.PrCode} {seg.Code}: column='{r.ColumnCode}' assignment='{r.AssignedCode}'");
            }
        }
        mismatches.Should().BeEmpty(
            "the PR dimension columns are the single truth and assignments are their projection — " +
            "a divergence means a write path skipped the projection hook");
    }

    private sealed record ProbeRow(string PrCode, string ColumnCode, string? AssignedCode);
}
