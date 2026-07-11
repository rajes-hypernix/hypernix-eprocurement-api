using eProcure.Domain.Procurement;
using eProcure.Domain.Sourcing;
using eProcure.Domain.Suppliers;
using eProcure.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace eProcure.Tests;

/// <summary>
/// Slice H T2 (AN-2): covers the Award→PO lineage backfill against real rows. Runs the SAME statements
/// the migration runs (<see cref="AwardPoLineageBackfill.Statements"/> — extracted, not replicated):
/// PO → Award via the legacy AwardCode; PO line → its AwardAllocation where the (award, vendor, line
/// code) match is unique. Proves the four outcomes: matched PO, uniquely-matched line, AMBIGUOUS line
/// (stays null — never guessed), and unmatched PO (stays null). Postgres-backed, rolled back.
/// </summary>
public sealed class AwardPoLineageBackfillTests
{
    private static string Conn =>
        Environment.GetEnvironmentVariable("ConnectionStrings__Default")
        ?? "Host=localhost;Port=5432;Database=eprocure;Username=eprocure;Password=localdev";

    private static AppDbContext NewCtx() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(Conn).Options);

    private static readonly DateTime Now = new(2026, 7, 11, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Backfill_links_matched_pos_and_unique_lines_and_leaves_ambiguous_or_unmatched_null()
    {
        await using var db = NewCtx();
        if (!await db.Database.CanConnectAsync())
            throw new InvalidOperationException("Backfill test requires the local Docker Postgres (docker compose up -d).");

        var sfx = Guid.NewGuid().ToString("N")[..8];
        await using var tx = await db.Database.BeginTransactionAsync();

        var vendor = new Vendor { Code = $"V-APL-{sfx}", Name = "APL", RegisteredName = "APL" };
        var rfq = new Rfq { Code = $"RFQ-APL-{sfx}", Title = "APL", CreatedUtc = Now, UpdatedUtc = Now };
        db.Vendors.Add(vendor);
        db.Rfqs.Add(rfq);

        // "Z" is allocated twice to the same vendor -> ambiguous for a PO line with ItemCode "Z".
        var allocX = new AwardAllocation { RfqLineCode = "X", VendorId = vendor.Id, Qty = 1, UnitPrice = 10 };
        var allocY = new AwardAllocation { RfqLineCode = "Y", VendorId = vendor.Id, Qty = 2, UnitPrice = 20 };
        var allocZ1 = new AwardAllocation { RfqLineCode = "Z", VendorId = vendor.Id, Qty = 3, UnitPrice = 30 };
        var allocZ2 = new AwardAllocation { RfqLineCode = "Z", VendorId = vendor.Id, Qty = 4, UnitPrice = 40 };
        var award = new Award
        {
            Code = $"AWD-APL-{sfx}", RfqId = rfq.Id, CreatedByUserId = "u_test", CreatedUtc = Now, UpdatedUtc = Now,
            Allocations = { allocX, allocY, allocZ1, allocZ2 },
        };
        db.Awards.Add(award);

        // Matched PO (AwardCode resolves) with a uniquely-matched line "X" and an ambiguous line "Z".
        var poMatched = new PurchaseOrder
        {
            Code = $"PO-APL-{sfx}", VendorId = vendor.Id, AwardCode = award.Code, CreatedUtc = Now, UpdatedUtc = Now,
            Lines = { new PoLine { ItemCode = "X" }, new PoLine { ItemCode = "Z" } },
        };
        // Unmatched PO — its AwardCode resolves to no Award, so AwardId must stay null.
        var poUnmatched = new PurchaseOrder
        {
            Code = $"PO-APLU-{sfx}", VendorId = vendor.Id, AwardCode = $"AWD-NOPE-{sfx}", CreatedUtc = Now, UpdatedUtc = Now,
            Lines = { new PoLine { ItemCode = "X" } },
        };
        db.PurchaseOrders.AddRange(poMatched, poUnmatched);
        await db.SaveChangesAsync();

        var lineX = poMatched.Lines.Single(l => l.ItemCode == "X").Id;
        var lineZ = poMatched.Lines.Single(l => l.ItemCode == "Z").Id;

        // Run the exact statements the migration runs (no format braces -> ExecuteSqlRaw is safe).
        foreach (var sql in AwardPoLineageBackfill.Statements)
            await db.Database.ExecuteSqlRawAsync(sql);

        var matchedAwardId = await db.PurchaseOrders.AsNoTracking().Where(p => p.Id == poMatched.Id).Select(p => p.AwardId).SingleAsync();
        var unmatchedAwardId = await db.PurchaseOrders.AsNoTracking().Where(p => p.Id == poUnmatched.Id).Select(p => p.AwardId).SingleAsync();
        var lineAllocs = await db.PurchaseOrders.AsNoTracking().Where(p => p.Id == poMatched.Id)
            .SelectMany(p => p.Lines).Select(l => new { l.Id, l.AwardAllocationId }).ToListAsync();

        matchedAwardId.Should().Be(award.Id, "the PO's AwardCode resolves to the Award");
        unmatchedAwardId.Should().BeNull("no Award has that code");
        lineAllocs.Single(l => l.Id == lineX).AwardAllocationId.Should().Be(allocX.Id, "line X has exactly one matching allocation");
        lineAllocs.Single(l => l.Id == lineZ).AwardAllocationId.Should().BeNull("line Z has two candidate allocations — ambiguous, never guessed");

        await tx.RollbackAsync();
    }
}
