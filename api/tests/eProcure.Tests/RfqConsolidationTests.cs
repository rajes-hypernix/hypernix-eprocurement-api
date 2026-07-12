using eProcure.Domain;
using eProcure.Domain.Sourcing;
using eProcure.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace eProcure.Tests;

/// <summary>
/// Slice C — Consolidate Lines backend: soft reservation (A3), un-reservation (D6), and the
/// release that writes one PrLineSourcing link per source and flips InDraftRfq → InRfq (A5/D8),
/// plus the stale-line block (E2).
/// </summary>
public class RfqConsolidationTests
{
    private static (RequisitionService reqs, RfqService rfqs, TestContext c) Build()
    {
        var c = TestContext.New();
        c.User.Roles = [eProcure.Domain.Identity.Roles.Buyer];
        return (new RequisitionService(c.Db, c.Clock, c.Codes, c.Audit, new SegmentProjection(c.Db, c.Clock), new EntryFormService(c.Db, c.Clock, c.User)),
                new RfqService(c.Db, c.Clock, c.Codes, c.Audit, c.User, new eProcure.Infrastructure.Services.CustomListService(c.Db, c.Clock), Microsoft.Extensions.Options.Options.Create(new eProcure.Application.Sourcing.RfqGovernanceOptions()), Microsoft.Extensions.Logging.Abstractions.NullLogger<eProcure.Infrastructure.Services.RfqService>.Instance), c);
    }

    private static async Task<PurchaseRequisition> SeedOpenPr(TestContext c, int lines = 2)
    {
        var pr = new PurchaseRequisition
        {
            Code = "PR-2026-0700", Requestor = "Aishah", Department = "Ops", Submitted = true,
            Lines = [.. Enumerable.Range(0, lines).Select(i => PrLine.Create($"ITEM-{i}", $"Item {i}", 5 + i, "Unit", 100m))],
            CreatedUtc = c.Clock.UtcNow, UpdatedUtc = c.Clock.UtcNow,
        };
        pr.RecomputeHeaderStatus();
        c.Db.PurchaseRequisitions.Add(pr);
        await c.Db.SaveChangesAsync();
        c.Db.ChangeTracker.Clear();
        return pr;
    }

    [Fact] // A3 — adding a line to the basket reserves it
    public async Task ReserveLine_OpenLine_GoesInDraftRfq_NoLinkYet()
    {
        var (reqs, _, c) = Build();
        var pr = await SeedOpenPr(c, 1);
        var result = await reqs.ReserveLineAsync(pr.Id, pr.Lines[0].Id);

        result.Lines[0].LifecycleStatus.Should().Be("InDraftRfq");
        (await c.Db.PrLineSourcings.CountAsync()).Should().Be(0);   // no link during reservation
    }

    [Fact] // D6 — removing a basket line returns it to Open
    public async Task UnreserveLine_ReservedLine_ReturnsToOpen()
    {
        var (reqs, _, c) = Build();
        var pr = await SeedOpenPr(c, 1);
        await reqs.ReserveLineAsync(pr.Id, pr.Lines[0].Id);
        var result = await reqs.UnreserveLineAsync(pr.Id, pr.Lines[0].Id);
        result.Lines[0].LifecycleStatus.Should().Be("Open");
    }

    [Fact] // A5 / D8 — releasing a consolidated RFQ writes one link per source and flips to InRfq
    public async Task Release_MergedConsolidatedLine_WritesLinkPerSource_AndFlipsInRfq()
    {
        var (reqs, rfqs, c) = Build();
        var pr = await SeedOpenPr(c, 2);
        var l0 = pr.Lines[0].Id; var l1 = pr.Lines[1].Id;

        // basket adds reserve both lines
        await reqs.ReserveLineAsync(pr.Id, l0);
        await reqs.ReserveLineAsync(pr.Id, l1);
        c.Db.ChangeTracker.Clear();

        // a draft RFQ with ONE merged line carrying both source PR-line ids (per-source qty kept)
        var rfq = new Rfq
        {
            Code = "RFQ-2026-0900",   // Status defaults to Draft
            ClosesUtc = c.Clock.UtcNow.AddDays(7),
            Lines = [new RfqLine { LineCode = "ITEM-0", ItemCode = "ITEM-0", Qty = 11, Uom = "Unit", SourcePrLineIds = [l0.ToString(), l1.ToString()] }],
            CreatedUtc = c.Clock.UtcNow, UpdatedUtc = c.Clock.UtcNow,
        };
        rfq.WithInvites(c.Clock.UtcNow, Guid.NewGuid());
        c.Db.Rfqs.Add(rfq);
        await c.Db.SaveChangesAsync();
        c.Db.ChangeTracker.Clear();

        await rfqs.ReleaseAsync(rfq.Id);

        var after = (await reqs.GetAsync(pr.Id))!;
        after.Lines.Should().OnlyContain(l => l.LifecycleStatus == "InRfq");
        after.HeaderStatus.Should().Be("Sourced");

        var links = await c.Db.PrLineSourcings.AsNoTracking().ToListAsync();
        links.Should().HaveCount(2);                                      // one link per source
        links.Should().OnlyContain(x => x.LinkStatus == LinkStatus.Active && x.RfqId == rfq.Id && x.RfqLineCode == "ITEM-0");
        links.Select(x => x.PrLineId).Should().BeEquivalentTo(new[] { l0, l1 });
        links.Select(x => x.QtySourced).Should().BeEquivalentTo(new[] { 5m, 6m });   // each source's own qty
    }

    [Fact] // E2 — a source line that is no longer claimable blocks the whole release
    public async Task Release_WhenSourceLineNoLongerReserved_IsBlocked()
    {
        var (reqs, rfqs, c) = Build();
        var pr = await SeedOpenPr(c, 1);
        var l0 = pr.Lines[0].Id;
        await reqs.ReserveLineAsync(pr.Id, l0);
        await reqs.UnreserveLineAsync(pr.Id, l0);   // line returns to Open — no longer reserved by this RFQ
        c.Db.ChangeTracker.Clear();

        var rfq = new Rfq
        {
            Code = "RFQ-2026-0901",   // Status defaults to Draft
            ClosesUtc = c.Clock.UtcNow.AddDays(7),
            Lines = [new RfqLine { LineCode = "ITEM-0", ItemCode = "ITEM-0", Qty = 5, Uom = "Unit", SourcePrLineIds = [l0.ToString()] }],
            CreatedUtc = c.Clock.UtcNow, UpdatedUtc = c.Clock.UtcNow,
        };
        rfq.WithInvites(c.Clock.UtcNow, Guid.NewGuid());
        c.Db.Rfqs.Add(rfq);
        await c.Db.SaveChangesAsync();
        c.Db.ChangeTracker.Clear();

        var act = () => rfqs.ReleaseAsync(rfq.Id);
        await act.Should().ThrowAsync<DomainRuleException>();
        (await c.Db.PrLineSourcings.CountAsync()).Should().Be(0);   // nothing committed
    }

    [Fact] // F2 — a legacy (Confirm-lines) RFQ with no provenance releases unchanged
    public async Task Release_LegacyRfqWithoutProvenance_Succeeds_NoLinks()
    {
        var (_, rfqs, c) = Build();
        var rfq = new Rfq
        {
            Code = "RFQ-2026-0902",   // Status defaults to Draft
            ClosesUtc = c.Clock.UtcNow.AddDays(7),
            Lines = [new RfqLine { LineCode = "X", ItemCode = "X", Qty = 1, Uom = "Unit" }],   // no SourcePrLineIds
            CreatedUtc = c.Clock.UtcNow, UpdatedUtc = c.Clock.UtcNow,
        };
        rfq.WithInvites(c.Clock.UtcNow, Guid.NewGuid());
        c.Db.Rfqs.Add(rfq);
        await c.Db.SaveChangesAsync();

        var result = await rfqs.ReleaseAsync(rfq.Id);
        result.Status.Should().Be("Open");
        (await c.Db.PrLineSourcings.CountAsync()).Should().Be(0);
    }
}
