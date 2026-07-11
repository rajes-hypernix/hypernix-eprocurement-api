using eProcure.Domain.Procurement;
using eProcure.Domain.Suppliers;
using eProcure.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace eProcure.Tests;

/// <summary>
/// Slice H T3: covers the Invoice→GRN lineage backfill against real rows, running the SAME statement
/// the migration runs (<see cref="InvoiceGrnLineageBackfill.Statements"/> — extracted, not replicated).
/// An invoice links to its receipt only where its PO has exactly one GRN; a PO with multiple partial
/// receipts is ambiguous (null), and a PO with no receipt is null. Postgres-backed, rolled back.
/// </summary>
public sealed class InvoiceGrnLineageBackfillTests
{
    private static string Conn =>
        Environment.GetEnvironmentVariable("ConnectionStrings__Default")
        ?? "Host=localhost;Port=5432;Database=eprocure;Username=eprocure;Password=localdev";

    private static AppDbContext NewCtx() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(Conn).Options);

    private static readonly DateTime Now = new(2026, 7, 11, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Backfill_links_invoices_only_where_the_po_has_exactly_one_grn()
    {
        await using var db = NewCtx();
        if (!await db.Database.CanConnectAsync())
            throw new InvalidOperationException("Backfill test requires the local Docker Postgres (docker compose up -d).");

        var sfx = Guid.NewGuid().ToString("N")[..8];
        await using var tx = await db.Database.BeginTransactionAsync();

        var vendor = new Vendor { Code = $"V-IGL-{sfx}", Name = "IGL", RegisteredName = "IGL" };
        db.Vendors.Add(vendor);

        PurchaseOrder Po(string tag) => new() { Code = $"PO-IGL-{tag}-{sfx}", VendorId = vendor.Id, CreatedUtc = Now, UpdatedUtc = Now };
        var poSingle = Po("S");   // one GRN
        var poMulti = Po("M");    // two GRNs -> ambiguous
        var poNone = Po("N");     // no GRN
        db.PurchaseOrders.AddRange(poSingle, poMulti, poNone);

        Asn Asn(string tag, Guid poId) => new() { Code = $"ASN-IGL-{tag}-{sfx}", PoId = poId, VendorId = vendor.Id, CreatedUtc = Now, UpdatedUtc = Now };
        Grn Grn(string tag, Guid poId, Guid asnId) => new() { Code = $"GRN-IGL-{tag}-{sfx}", PoId = poId, AsnId = asnId, ReceivedBy = "t", CreatedUtc = Now };
        var asnS = Asn("S", poSingle.Id); var asnMa = Asn("Ma", poMulti.Id); var asnMb = Asn("Mb", poMulti.Id);
        db.Asns.AddRange(asnS, asnMa, asnMb);
        var grnS = Grn("S", poSingle.Id, asnS.Id);
        var grnMa = Grn("Ma", poMulti.Id, asnMa.Id);
        var grnMb = Grn("Mb", poMulti.Id, asnMb.Id);
        db.Grns.AddRange(grnS, grnMa, grnMb);

        Invoice Inv(string tag, Guid poId) => new() { Code = $"INV-IGL-{tag}-{sfx}", PoId = poId, VendorId = vendor.Id, Date = "11/07/2026", CreatedUtc = Now, UpdatedUtc = Now };
        var invSingle = Inv("S", poSingle.Id);
        var invMulti = Inv("M", poMulti.Id);
        var invNone = Inv("N", poNone.Id);
        db.Invoices.AddRange(invSingle, invMulti, invNone);
        await db.SaveChangesAsync();

        // Run the exact statement the migration runs (no format braces -> ExecuteSqlRaw is safe).
        foreach (var sql in InvoiceGrnLineageBackfill.Statements)
            await db.Database.ExecuteSqlRawAsync(sql);

        async Task<Guid?> GrnOf(Guid invId) =>
            await db.Invoices.AsNoTracking().Where(i => i.Id == invId).Select(i => i.GrnId).SingleAsync();

        (await GrnOf(invSingle.Id)).Should().Be(grnS.Id, "the PO has exactly one GRN");
        (await GrnOf(invMulti.Id)).Should().BeNull("the PO has two GRNs — ambiguous, never guessed");
        (await GrnOf(invNone.Id)).Should().BeNull("the PO has no GRN");

        await tx.RollbackAsync();
    }
}
