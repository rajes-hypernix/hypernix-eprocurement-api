using eProcure.Application;
using eProcure.Application.Sourcing;
using eProcure.Domain;
using eProcure.Domain.Sourcing;
using eProcure.Domain.Suppliers;
using eProcure.Infrastructure.Persistence;
using eProcure.Infrastructure.Services;
using eProcure.Infrastructure.Seed;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace eProcure.Tests;

/// <summary>
/// Slice I service/integration coverage: reason-code validation against the seeded Custom Lists,
/// RfqEvent writing in the same transaction, extension count derived from events, vendor-scoped
/// actions, the BidService invitation guard (E14), and the derived legacy fields excluding Rescinded.
/// </summary>
public class RfqGovernanceServiceTests
{
    private static RfqService Buyer(TestContext c) => new(
        c.Db, c.Clock, c.Codes, c.Audit, c.User,
        new CustomListService(c.Db, c.Clock),
        Options.Create(new RfqGovernanceOptions { MaxExtensions = 2, MinRemainingHoursForLateInvite = 72 }), Microsoft.Extensions.Logging.Abstractions.NullLogger<eProcure.Infrastructure.Services.RfqService>.Instance);

    private static RfqVendorService Vendor(TestContext c) => new(
        c.Db, c.Clock, c.Audit, c.User, new CustomListService(c.Db, c.Clock));

    private static async Task<(TestContext c, Rfq rfq, Vendor vA, Vendor vB)> SeedOpenRfq()
    {
        var c = TestContext.New();
        c.Db.CustomLists.AddRange(CustomListSeed.All(c.Clock.UtcNow));   // includes the RFQ reason lists
        var vA = new Vendor { Code = "V-A", Name = "Alpha", RegisteredName = "Alpha" };
        var vB = new Vendor { Code = "V-B", Name = "Beta", RegisteredName = "Beta" };
        c.Db.Vendors.AddRange(vA, vB);
        var rfq = new Rfq
        {
            Code = "RFQ-2026-0500", Title = "Pumps",
            ClosesUtc = c.Clock.UtcNow.AddDays(10), OriginalClosesUtc = c.Clock.UtcNow.AddDays(10),
            CreatedUtc = c.Clock.UtcNow, UpdatedUtc = c.Clock.UtcNow,
        }.SeededAs(RfqStatus.Open);
        rfq.WithInvites(c.Clock.UtcNow, vA.Id, vB.Id);
        c.Db.Rfqs.Add(rfq);
        await c.Db.SaveChangesAsync();
        return (c, rfq, vA, vB);
    }

    [Fact]
    public async Task Rescind_WithValidReason_WritesEvent_AndExcludesFromInvitedIds()
    {
        var (c, rfq, vA, _) = await SeedOpenRfq();
        var detail = await Buyer(c).RescindInvitationAsync(rfq.Id, vA.Id, new RescindInvitationRequest("DUPLICATE", "dup"));

        detail.InvitedVendorIds.Should().NotContain(vA.Id.ToString());          // derived list excludes Rescinded
        detail.Invitations.Should().Contain(i => i.VendorId == vA.Id.ToString() && i.Status == "Rescinded");  // still visible
        (await c.Db.RfqEvents.CountAsync(e => e.EventType == RfqEventType.InvitationRescinded)).Should().Be(1);
    }

    [Fact]
    public async Task Rescind_WithUnknownReasonCode_Is409()
    {
        var (c, rfq, vA, _) = await SeedOpenRfq();
        var act = () => Buyer(c).RescindInvitationAsync(rfq.Id, vA.Id, new RescindInvitationRequest("NOPE", null));
        await act.Should().ThrowAsync<DomainRuleException>().WithMessage("*not a valid*");
    }

    [Fact]
    public async Task Extend_CapsAtMaxExtensions_CountingEvents()
    {
        var (c, rfq, _, _) = await SeedOpenRfq();
        var svc = Buyer(c);
        await svc.ExtendAsync(rfq.Id, new ExtendRfqRequest(c.Clock.UtcNow.AddDays(20), "LOW_RESPONSE", null));
        await svc.ExtendAsync(rfq.Id, new ExtendRfqRequest(c.Clock.UtcNow.AddDays(30), "HOLIDAY", null));
        var third = () => svc.ExtendAsync(rfq.Id, new ExtendRfqRequest(c.Clock.UtcNow.AddDays(40), "OTHER", null));

        await third.Should().ThrowAsync<DomainRuleException>().WithMessage("*maximum of 2*");
        (await c.Db.RfqEvents.CountAsync(e => e.EventType == RfqEventType.Extended)).Should().Be(2);
        (await c.Db.Rfqs.FirstAsync()).OriginalClosesUtc.Should().Be(c.Clock.UtcNow.AddDays(10));   // immutable
    }

    [Fact]
    public async Task Invite_WhileOpen_WritesVendorInvitedEvent()
    {
        var (c, rfq, _, _) = await SeedOpenRfq();
        var vC = new Vendor { Code = "V-C", Name = "Gamma", RegisteredName = "Gamma" };
        c.Db.Vendors.Add(vC); await c.Db.SaveChangesAsync();

        var detail = await Buyer(c).InviteVendorAsync(rfq.Id, new InviteVendorRequest(vC.Id.ToString()));
        detail.InvitedVendorIds.Should().Contain(vC.Id.ToString());
        (await c.Db.RfqEvents.CountAsync(e => e.EventType == RfqEventType.VendorInvited)).Should().Be(1);
    }

    [Fact]
    public async Task Vendor_DeclineThenReverse_WritesEvents_AndScopes()
    {
        var (c, rfq, vA, _) = await SeedOpenRfq();
        c.User.VendorId = vA.Id;   // act as vendor A
        var v = Vendor(c);

        await v.DeclineAsync(rfq.Id, new DeclineInvitationRequest("CAPACITY", "busy"));
        (await c.Db.RfqInvitations.FirstAsync(i => i.VendorId == vA.Id)).Status.Should().Be(RfqInvitationStatus.Declined);

        await v.IntendAsync(rfq.Id);   // T4 reversal
        (await c.Db.RfqInvitations.FirstAsync(i => i.VendorId == vA.Id)).Status.Should().Be(RfqInvitationStatus.IntendToBid);
        (await c.Db.RfqEvents.CountAsync(e => e.EventType == RfqEventType.VendorDeclined)).Should().Be(1);
        (await c.Db.RfqEvents.CountAsync(e => e.EventType == RfqEventType.DeclineReversed)).Should().Be(1);
    }

    [Fact]
    public async Task Bid_DeclinedVendor_CannotSubmit_UntilReversed_E14()
    {
        var (c, rfq, vA, _) = await SeedOpenRfq();
        c.User.VendorId = vA.Id;
        await Vendor(c).DeclineAsync(rfq.Id, new DeclineInvitationRequest("COMMERCIAL", null));

        var bids = new BidService(c.Db, c.Clock, c.Codes, c.Audit, c.User);
        var req = new SaveBidRequest(0, 0, [new BidLineDto("PUMP", true, 100, 4, false, null)], [], []);
        var act = () => bids.SubmitAsync(rfq.Id, req);
        await act.Should().ThrowAsync<DomainRuleException>().WithMessage("*Reverse your decline*");
    }

    [Fact]
    public async Task Bid_RescindedVendor_IsForbidden()
    {
        var (c, rfq, vA, _) = await SeedOpenRfq();
        await Buyer(c).RescindInvitationAsync(rfq.Id, vA.Id, new RescindInvitationRequest("COMPLIANCE", null));
        c.User.VendorId = vA.Id;

        var bids = new BidService(c.Db, c.Clock, c.Codes, c.Audit, c.User);
        var req = new SaveBidRequest(0, 0, [new BidLineDto("PUMP", true, 100, 4, false, null)], [], []);
        var act = () => bids.SubmitAsync(rfq.Id, req);
        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*not invited*");
    }

    // A DbContext that fails every write — models a transient failure of the mark-viewed side effect.
    private sealed class ThrowingSaveDb(Microsoft.EntityFrameworkCore.DbContextOptions<AppDbContext> o) : AppDbContext(o)
    {
        public override Task<int> SaveChangesAsync(CancellationToken ct = default) =>
            throw new InvalidOperationException("simulated view-marker write failure");
    }

    [Fact]
    public async Task GetAsync_WhenMarkViewedWriteFails_StillReturnsTheRfq()
    {
        // Condition 1 regression: a vendor's GET fires the passive mark-viewed write; if that write
        // throws, the read must still succeed (the page must not fail to load).
        var vendorId = Guid.NewGuid();
        var opts = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"markviewed-{Guid.NewGuid()}").Options;
        var clock = new FakeClock(new DateTime(2026, 6, 28, 9, 0, 0, DateTimeKind.Utc));

        await using (var seed = new AppDbContext(opts))
        {
            // mark-viewed matches the invitation by VendorId only, so no Vendor row is needed.
            var rfq = new Rfq { Code = "RFQ-2026-0600", Title = "Pumps", ClosesUtc = clock.UtcNow.AddDays(5), CreatedUtc = clock.UtcNow, UpdatedUtc = clock.UtcNow }.SeededAs(RfqStatus.Open);
            rfq.WithInvites(clock.UtcNow, vendorId);   // status Invited → first view would write
            seed.Rfqs.Add(rfq);
            await seed.SaveChangesAsync();
        }

        await using var throwing = new ThrowingSaveDb(opts);
        var user = new FakeCurrentUser("VU-x", "X", vendorId: vendorId);
        var svc = new RfqService(throwing, clock, new CodeGenerator(throwing, clock), new AuditLogWriter(throwing, clock, user),
            user, new CustomListService(throwing, clock),
            Options.Create(new RfqGovernanceOptions()), Microsoft.Extensions.Logging.Abstractions.NullLogger<RfqService>.Instance);

        var rfqId = await throwing.Rfqs.Select(r => r.Id).FirstAsync();
        var detail = await svc.GetAsync(rfqId);      // must NOT throw despite the failing view-marker write

        detail.Should().NotBeNull();
        detail!.Code.Should().Be("RFQ-2026-0600");
    }
}
