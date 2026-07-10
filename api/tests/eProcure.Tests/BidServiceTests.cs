using eProcure.Application;
using eProcure.Application.Sourcing;
using eProcure.Domain;
using eProcure.Domain.Sourcing;
using eProcure.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace eProcure.Tests;

public sealed class BidServiceTests
{
    private static readonly Guid VendorA = Guid.NewGuid();
    private static readonly Guid VendorB = Guid.NewGuid();

    private static async Task<(BidService Svc, TestContext C, Guid RfqId)> SetupAsync(
        RfqStatus status, DateTime? closes, Guid actingVendor)
    {
        var c = TestContext.New(actorId: "VU-a", actorName: "Vendor A");
        c.User.Roles = [eProcure.Domain.Identity.Roles.Vendor];
        c.User.VendorId = actingVendor;

        var rfq = new Rfq
        {
            Code = "RFQ-2026-0099",
            Title = "Test",
            Envelope = RfqEnvelope.Single,
            Status = status,
            ClosesUtc = closes,
            Lines = [new RfqLine { ItemCode = "PIP-CS6-SCH40", Description = "Pipe", Qty = 120, Uom = "Length" }],
            CreatedUtc = c.Clock.UtcNow,
            UpdatedUtc = c.Clock.UtcNow,
        };
        rfq.WithInvites(c.Clock.UtcNow, VendorA, VendorB);
        c.Db.Rfqs.Add(rfq);
        await c.Db.SaveChangesAsync();

        var svc = new BidService(c.Db, c.Clock, c.Codes, c.Audit, c.User);
        return (svc, c, rfq.Id);
    }

    private static SaveBidRequest Priced() =>
        new(Lead: 5, Warranty: 12,
            Lines: [new BidLineDto("PIP-CS6-SCH40", true, 165m, 120, false, null)],
            Answers: [], Files: ["quote.pdf"]);

    [Fact]
    public async Task Submit_OnOpenRfqBeforeClose_Succeeds_AndAudits()
    {
        var (svc, c, rfqId) = await SetupAsync(RfqStatus.Open, new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc), VendorA);
        var bid = await svc.SubmitAsync(rfqId, Priced());

        bid.Submitted.Should().BeTrue();
        (await c.Db.AuditEntries.AnyAsync(a => a.EntityType == "Bid" && a.Action == "Bid submitted"))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Submit_AfterRfqClosed_IsRejected()
    {
        var (svc, _, rfqId) = await SetupAsync(RfqStatus.Closed, DateTime.UtcNow.AddDays(-1), VendorA);
        var act = () => svc.SubmitAsync(rfqId, Priced());
        await act.Should().ThrowAsync<DomainRuleException>().WithMessage("*closed*");
    }

    [Fact]
    public async Task Submit_PastClosesUtc_EvenIfStatusOpen_IsRejected()
    {
        // Status still Open but the deadline has passed → reject (deadline is a timestamp).
        var (svc, c, rfqId) = await SetupAsync(RfqStatus.Open, null, VendorA);
        var rfq = await c.Db.Rfqs.FirstAsync();
        rfq.ClosesUtc = c.Clock.UtcNow.AddMinutes(-1);
        await c.Db.SaveChangesAsync();

        var act = () => svc.SubmitAsync(rfqId, Priced());
        await act.Should().ThrowAsync<DomainRuleException>().WithMessage("*closed*");
    }

    [Fact]
    public async Task GetMyBid_OnlyReturnsOwnVendorsBid_NotAnotherVendors()
    {
        // Vendor A submits.
        var (svcA, c, rfqId) = await SetupAsync(RfqStatus.Open, null, VendorA);
        await svcA.SubmitAsync(rfqId, Priced());

        // Vendor B (same DB) sees no bid of its own — never vendor A's.
        c.User.VendorId = VendorB;
        var svcB = new BidService(c.Db, c.Clock, c.Codes, c.Audit, c.User);
        var bForB = await svcB.GetMyBidAsync(rfqId);
        bForB.Should().BeNull();

        // And there is exactly one bid stored, owned by vendor A.
        var stored = await c.Db.Bids.SingleAsync();
        stored.VendorId.Should().Be(VendorA);
    }

    [Fact]
    public async Task Submit_ByNonVendorPrincipal_IsForbidden()
    {
        var (svc, c, rfqId) = await SetupAsync(RfqStatus.Open, null, VendorA);
        c.User.VendorId = null; // internal user, not a vendor
        var act = () => svc.SubmitAsync(rfqId, Priced());
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Bid_ByUninvitedVendor_IsForbidden()
    {
        var (svc, c, rfqId) = await SetupAsync(RfqStatus.Open, null, Guid.NewGuid() /* not invited */);
        var act = () => svc.SaveDraftAsync(rfqId, Priced());
        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*not invited*");
    }
}
