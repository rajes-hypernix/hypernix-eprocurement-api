using eProcure.Application.Abstractions;
using eProcure.Application.Sourcing;
using eProcure.Domain;
using eProcure.Domain.Identity;
using eProcure.Domain.Sourcing;
using eProcure.Domain.Suppliers;
using eProcure.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace eProcure.Tests;

/// <summary>
/// Slice D — integration hooks: award settles PR-line provenance (A6/A7/A8), RFQ cancel returns
/// sourced lines (A9 released / A4 draft), and a line cannot be reserved twice (E1). Lineage is
/// preserved on every path (links are closed, never deleted).
/// </summary>
public class PrIntegrationTests
{
    private sealed class NoopNetSuite : INetSuiteClient
    {
        public Task PushPurchaseOrderAsync(string c, CancellationToken ct = default) => Task.CompletedTask;
        public Task PushVendorBillAsync(string c, CancellationToken ct = default) => Task.CompletedTask;
        public Task PushItemReceiptAsync(string c, CancellationToken ct = default) => Task.CompletedTask;
        public Task PushBillPaymentAsync(string c, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static RequisitionService Reqs(TestContext c) => new(c.Db, c.Clock, c.Codes, c.Audit);
    private static RfqService Rfqs(TestContext c) => new(c.Db, c.Clock, c.Codes, c.Audit, c.User, new eProcure.Infrastructure.Services.CustomListService(c.Db, c.Clock), Microsoft.Extensions.Options.Options.Create(new eProcure.Application.Sourcing.RfqGovernanceOptions()), Microsoft.Extensions.Logging.Abstractions.NullLogger<eProcure.Infrastructure.Services.RfqService>.Instance);

    private static async Task<PurchaseRequisition> SeedPr(TestContext c, string code, params (string item, decimal qty, PrLineStatus st)[] lines)
    {
        var pr = new PurchaseRequisition
        {
            Code = code, Requestor = "Seed", Department = "Ops", Submitted = true,
            Lines = [.. lines.Select(l => PrLine.Create(l.item, l.item, l.qty, "Unit", 100m, l.st))],
            CreatedUtc = c.Clock.UtcNow, UpdatedUtc = c.Clock.UtcNow,
        };
        pr.RecomputeHeaderStatus();
        c.Db.PurchaseRequisitions.Add(pr);
        await c.Db.SaveChangesAsync();
        return pr;
    }

    // ---- A6 + A7/A8: award settlement ----
    [Fact]
    public async Task ApproveAward_AwardsWinningSourceLine_AndReturnsTheRest()
    {
        var c = TestContext.New(actorId: "u_faridah", actorName: "Faridah");
        c.User.Roles = [Roles.Buyer];
        var va = new Vendor { Code = "V-A", Name = "Alpha", RegisteredName = "Alpha" };
        c.Db.Vendors.Add(va);
        await c.Db.SaveChangesAsync();

        var now = c.Clock.UtcNow;
        var rfq = new Rfq
        {
            Code = "RFQ-2026-0950", Envelope = RfqEnvelope.Dual,
            TechnicalOpened = true, TechFinalized = true, CommercialOpened = true,
            TechnicalEvaluatorIds = ["u_hafiz"],
            Lines = [new RfqLine { LineCode = "PUMP", ItemCode = "PUMP", Description = "Pump", Qty = 4, Uom = "Unit" },
                     new RfqLine { LineCode = "VALVE", ItemCode = "VALVE", Description = "Valve", Qty = 2, Uom = "Unit" }],
            CreatedUtc = now, UpdatedUtc = now,
        }.SeededAs(RfqStatus.Evaluation);
        rfq.WithInvites(now, va.Id);
        c.Db.Rfqs.Add(rfq);
        c.Db.Bids.Add(new Bid
        {
            Code = "BID-A", RfqId = rfq.Id, VendorId = va.Id, Submitted = true,
            Lines = [new BidLine { ItemCode = "PUMP", Bidding = true, Price = 100, Qty = 4 },
                     new BidLine { ItemCode = "VALVE", Bidding = true, Price = 50, Qty = 2 }],
            CreatedUtc = now, UpdatedUtc = now,
        });
        foreach (var crit in TechnicalCriteria.All)
            c.Db.TechnicalScores.Add(new TechnicalScore(rfq.Id, va.Id, "u_hafiz", crit.Key, 88));

        var pr = await SeedPr(c, "PR-2026-0800", ("PUMP", 4, PrLineStatus.InRfq), ("VALVE", 2, PrLineStatus.InRfq));
        var pump = pr.Lines.First(l => l.ItemCode == "PUMP");
        var valve = pr.Lines.First(l => l.ItemCode == "VALVE");
        c.Db.PrLineSourcings.Add(new PrLineSourcing(pump.Id, rfq.Id, "PUMP", 4, now));
        c.Db.PrLineSourcings.Add(new PrLineSourcing(valve.Id, rfq.Id, "VALVE", 2, now));
        await c.Db.SaveChangesAsync();
        c.Db.ChangeTracker.Clear();

        var svc = new AwardService(c.Db, c.Clock, c.Codes, c.Audit, new NoopNetSuite(), c.User);
        await svc.SubmitForApprovalAsync(rfq.Id, new SubmitAwardRequest([new AllocationInput("PUMP", va.Id, 4)]));   // award PUMP only
        c.User.UserId = "u_lim"; c.User.UserName = "Lim"; c.User.Roles = [Roles.Buyer, Roles.Approver];
        var award = await c.Db.Awards.FirstAsync();
        await svc.ApproveAsync(award.Id);

        var after = (await Reqs(c).GetAsync(pr.Id))!;
        after.Lines.Single(l => l.ItemCode == "PUMP").LifecycleStatus.Should().Be("Awarded");
        after.Lines.Single(l => l.ItemCode == "VALVE").LifecycleStatus.Should().Be("Open");   // not awarded → returned to Open

        var links = await c.Db.PrLineSourcings.AsNoTracking().ToListAsync();
        links.Single(x => x.RfqLineCode == "PUMP").LinkStatus.Should().Be(LinkStatus.Active);     // awarded line keeps its link
        var valveLink = links.Single(x => x.RfqLineCode == "VALVE");
        valveLink.LinkStatus.Should().Be(LinkStatus.Returned);
        valveLink.Reason.Should().Be("not awarded / residual");
        valveLink.ClosedUtc.Should().NotBeNull();
    }

    // ---- A9: cancelling a released RFQ returns InRfq lines, closes links Cancelled ----
    [Fact]
    public async Task CancelReleasedRfq_ReturnsInRfqLines_LinksCancelled()
    {
        var c = TestContext.New(); c.User.Roles = [Roles.Buyer];
        var now = c.Clock.UtcNow;
        var pr = await SeedPr(c, "PR-2026-0801", ("A", 5, PrLineStatus.InRfq));
        var line = pr.Lines[0];
        var rfq = new Rfq
        {
            Code = "RFQ-2026-0951", ClosesUtc = now.AddDays(1),
            Lines = [new RfqLine { LineCode = "A", ItemCode = "A", Qty = 5, Uom = "Unit", SourcePrLineIds = [line.Id.ToString()] }],
            CreatedUtc = now, UpdatedUtc = now,
        }.SeededAs(RfqStatus.Open);
        c.Db.Rfqs.Add(rfq);
        c.Db.PrLineSourcings.Add(new PrLineSourcing(line.Id, rfq.Id, "A", 5, now));
        await c.Db.SaveChangesAsync();
        c.Db.ChangeTracker.Clear();

        await Rfqs(c).CancelAsync(rfq.Id);

        (await Reqs(c).GetAsync(pr.Id))!.Lines[0].LifecycleStatus.Should().Be("Open");
        var link = await c.Db.PrLineSourcings.AsNoTracking().FirstAsync();
        link.LinkStatus.Should().Be(LinkStatus.Cancelled);     // retained, not deleted (E3)
        link.Reason.Should().Be("RFQ cancelled");
    }

    // ---- A4: cancelling a still-draft RFQ releases its InDraftRfq lines (no link was written) ----
    [Fact]
    public async Task CancelDraftRfq_ReleasesInDraftLines_NoLink()
    {
        var c = TestContext.New(); c.User.Roles = [Roles.Buyer];
        var now = c.Clock.UtcNow;
        var pr = await SeedPr(c, "PR-2026-0802", ("A", 5, PrLineStatus.InDraftRfq));
        var line = pr.Lines[0];
        var rfq = new Rfq
        {
            Code = "RFQ-2026-0952",   // Status defaults to Draft
            Lines = [new RfqLine { LineCode = "A", ItemCode = "A", Qty = 5, Uom = "Unit", SourcePrLineIds = [line.Id.ToString()] }],
            CreatedUtc = now, UpdatedUtc = now,
        };
        c.Db.Rfqs.Add(rfq);
        await c.Db.SaveChangesAsync();
        c.Db.ChangeTracker.Clear();

        await Rfqs(c).CancelAsync(rfq.Id);

        (await Reqs(c).GetAsync(pr.Id))!.Lines[0].LifecycleStatus.Should().Be("Open");
        (await c.Db.PrLineSourcings.CountAsync()).Should().Be(0);    // never any link for a draft
    }

    // ---- E1: a line already reserved by one basket cannot be reserved by another ----
    [Fact]
    public async Task ReserveLine_AlreadyReserved_IsBlocked()
    {
        var c = TestContext.New(); c.User.Roles = [Roles.Buyer];
        var pr = await SeedPr(c, "PR-2026-0803", ("A", 5, PrLineStatus.Open));
        c.Db.ChangeTracker.Clear();
        var reqs = Reqs(c);
        await reqs.ReserveLineAsync(pr.Id, pr.Lines[0].Id);
        var act = () => reqs.ReserveLineAsync(pr.Id, pr.Lines[0].Id);
        await act.Should().ThrowAsync<DomainRuleException>();
    }
}
