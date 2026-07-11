using eProcure.Domain;
using eProcure.Domain.Procurement;
using eProcure.Domain.Sourcing;
using eProcure.Infrastructure.Persistence;
using FluentAssertions;
using FluentAssertions.Extensions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace eProcure.Tests;

/// <summary>
/// Slice H T5 (AN-3): transition timestamps are stamped INSIDE the domain transition method (the single
/// place a transition happens), from the time the service passes in via IClock. The unit tests prove the
/// stamp lands and — critically — that early close records the ACTUAL close without touching the PLANNED
/// ClosesUtc / OriginalClosesUtc. The backfill test proves only the clean sources populate.
/// </summary>
public sealed class TransitionTimestampsTests
{
    private static readonly DateTime T = new(2026, 7, 11, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Rfq_release_then_early_close_stamps_actuals_and_never_touches_the_planned_close()
    {
        var planned = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var rfq = new Rfq { Code = "R", Title = "t", ClosesUtc = planned, OriginalClosesUtc = planned, CreatedUtc = T, UpdatedUtc = T };

        rfq.MarkReleased(T);
        rfq.Status.Should().Be(RfqStatus.Open);
        rfq.ReleasedUtc.Should().Be(T);

        var closedAt = T.AddHours(5);
        rfq.CloseEarly(closedAt);
        rfq.Status.Should().Be(RfqStatus.Closed);
        rfq.ClosedUtc.Should().Be(closedAt);
        rfq.ClosesUtc.Should().Be(planned, "early close records ClosedUtc, never overwrites the planned close");
        rfq.OriginalClosesUtc.Should().Be(planned, "Slice I's baseline is untouched");
    }

    [Fact]
    public void Rfq_award_po_invoice_asn_stamp_their_transition_instants()
    {
        var rfq = new Rfq { Code = "R", Title = "t", CreatedUtc = T, UpdatedUtc = T };
        rfq.MarkAwarded(T);
        rfq.AwardedUtc.Should().Be(T);

        var po = new PurchaseOrder { Code = "P", CreatedUtc = T, UpdatedUtc = T };
        po.Issue(T);
        po.IssuedUtc.Should().Be(T);
        var ackAt = T.AddHours(1);
        po.Acknowledge(ackAt);
        po.AcknowledgedUtc.Should().Be(ackAt);

        var inv = new Invoice { Code = "I", CreatedUtc = T, UpdatedUtc = T };
        inv.MarkSubmitted(T);
        inv.SubmittedUtc.Should().Be(T);
        var apprAt = T.AddHours(2);
        inv.Approve(apprAt);
        inv.ApprovedUtc.Should().Be(apprAt);

        var asn = new Asn { Code = "A", CreatedUtc = T, UpdatedUtc = T };
        asn.MarkReceived(T);
        asn.ReceivedUtc.Should().Be(T);
    }

    // ---- Backfill (Postgres-backed): only clean sources populate ----

    private static string Conn =>
        Environment.GetEnvironmentVariable("ConnectionStrings__Default")
        ?? "Host=localhost;Port=5432;Database=eprocure;Username=eprocure;Password=localdev";

    private static AppDbContext NewCtx() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(Conn).Options);

    [Fact]
    public async Task Backfill_populates_only_clean_sources_and_leaves_the_rest_null()
    {
        await using var db = NewCtx();
        if (!await db.Database.CanConnectAsync())
            throw new InvalidOperationException("Backfill test requires the local Docker Postgres (docker compose up -d).");

        var sfx = Guid.NewGuid().ToString("N")[..8];
        await using var tx = await db.Database.BeginTransactionAsync();

        var releasedAt = new DateTime(2026, 7, 8, 9, 0, 0, DateTimeKind.Utc);
        var submittedAt = new DateTime(2026, 7, 9, 14, 0, 0, DateTimeKind.Utc);

        var rfq = new Rfq { Code = $"RFQ-TT-{sfx}", Title = "TT", CreatedUtc = T, UpdatedUtc = T };
        db.Rfqs.Add(rfq);
        var pr = new PurchaseRequisition { Code = $"PR-TT-{sfx}", CreatedUtc = T, UpdatedUtc = T };
        db.PurchaseRequisitions.Add(pr);
        await db.SaveChangesAsync();

        db.RfqEvents.Add(RfqEvent.Create(rfq.Id, RfqEventType.Released, releasedAt, actorUserId: "u_test"));
        // Typed transition audit for the PR submit (EntityId is the PR Code) — the clean PR source.
        db.AuditEntries.Add(new AuditEntry("PurchaseRequisition", pr.Code, "PR submitted", null, "Submitted",
            "u_test", "Tester", submittedAt, fromState: "Draft", toState: "Submitted"));
        await db.SaveChangesAsync();

        foreach (var sql in TransitionTimestampsBackfill.Statements)
            await db.Database.ExecuteSqlRawAsync(sql);

        var r = await db.Rfqs.AsNoTracking().Where(x => x.Id == rfq.Id).Select(x => new { x.ReleasedUtc, x.ClosedUtc, x.AwardedUtc }).SingleAsync();
        var submitted = await db.PurchaseRequisitions.AsNoTracking().Where(x => x.Id == pr.Id).Select(x => x.SubmittedUtc).SingleAsync();

        r.ReleasedUtc.Should().BeCloseTo(releasedAt, 1.Seconds(), "clean source: the Released RfqEvent");
        submitted.Should().BeCloseTo(submittedAt, 1.Seconds(), "clean source: the typed PR-submit AuditEntry");
        r.ClosedUtc.Should().BeNull("no Closed event exists — never fabricated");
        r.AwardedUtc.Should().BeNull("AwardedUtc has no clean source (below-clean generic audit) — stays null");

        await tx.RollbackAsync();
    }
}
