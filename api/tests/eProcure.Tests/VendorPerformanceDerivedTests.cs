using eProcure.Domain.Procurement;
using eProcure.Domain.Suppliers;
using eProcure.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace eProcure.Tests;

/// <summary>
/// Slice H T7 (AN-4/DBA-10): VendorPerformance is derived from facts, not stored. Seeds a vendor with a
/// receipted, issued PO and a paid invoice, then reads the VendorPerformanceView and asserts the honest
/// split: Otd/Breaches are NULL (no computable source), the metrics with facts compute, and the metrics
/// with no facts (Response/WinRate — no invitations) are NULL, not zero. Postgres-backed, rolled back.
/// </summary>
public sealed class VendorPerformanceDerivedTests
{
    private static string Conn =>
        Environment.GetEnvironmentVariable("ConnectionStrings__Default")
        ?? "Host=localhost;Port=5432;Database=eprocure;Username=eprocure;Password=localdev";

    private static AppDbContext NewCtx() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(Conn).Options);

    // Recent dates so the view's rolling-12-month / current-year windows include them.
    private static readonly DateTime IssuedUtc = new(2026, 7, 1, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Received = new(2026, 7, 6);   // 5 days after issue

    [Fact]
    public async Task View_derives_metrics_from_facts_and_nulls_what_it_cannot_compute()
    {
        await using var db = NewCtx();
        if (!await db.Database.CanConnectAsync())
            throw new InvalidOperationException("Derived-performance test requires the local Docker Postgres (docker compose up -d).");

        var sfx = Guid.NewGuid().ToString("N")[..8];
        await using var tx = await db.Database.BeginTransactionAsync();

        var v = new Vendor { Code = $"V-PERF-{sfx}", Name = "PerfCo", RegisteredName = "PerfCo" };
        db.Vendors.Add(v);

        var po = new PurchaseOrder { Code = $"PO-PERF-{sfx}", VendorId = v.Id, CreatedUtc = IssuedUtc, UpdatedUtc = IssuedUtc };
        po.Issue(IssuedUtc);   // Draft -> Issued, stamps IssuedUtc (T5)
        db.PurchaseOrders.Add(po);

        var asn = new Asn { Code = $"ASN-PERF-{sfx}", PoId = po.Id, VendorId = v.Id, CreatedUtc = IssuedUtc, UpdatedUtc = IssuedUtc };
        db.Asns.Add(asn);
        db.Grns.Add(new Grn
        {
            Code = $"GRN-PERF-{sfx}", PoId = po.Id, AsnId = asn.Id, ReceivedDate = Received, ReceivedBy = "t", CreatedUtc = IssuedUtc,
            Lines = { new GrnLine { ItemCode = "X", ExpectedQty = 2, ReceivedQty = 2, Condition = "Good" } },
        });
        db.Invoices.Add(new Invoice
        {
            Code = $"INV-PERF-{sfx}", PoId = po.Id, VendorId = v.Id, Date = Received, CreatedUtc = IssuedUtc, UpdatedUtc = IssuedUtc,
            Lines = { new InvoiceLine { ItemCode = "X", Qty = 2, UnitPrice = 100 } },
        }.SeededAs(InvoiceStatus.Paid));
        await db.SaveChangesAsync();

        var perf = await db.VendorPerformance.AsNoTracking().SingleAsync(p => p.VendorId == v.Id);

        perf.Otd.Should().BeNull("no promised-delivery date exists to measure on-time against");
        perf.Breaches.Should().BeNull("a short receipt is not a modelled compliance breach");
        perf.Quality.Should().Be(100, "the one GRN line was received Good");
        perf.LeadDays.Should().Be(5, "goods received 5 days after the PO was issued");
        perf.SpendYtd.Should().Be(200m, "one paid invoice line, 2 x 100");
        perf.Pos.Should().Be(1, "one non-Draft PO");
        perf.Response.Should().BeNull("no invitations — null, not zero");
        perf.WinRate.Should().BeNull("no invitations — null, not zero");

        await tx.RollbackAsync();
    }
}
