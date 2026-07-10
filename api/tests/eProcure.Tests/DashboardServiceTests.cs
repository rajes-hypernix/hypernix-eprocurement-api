using eProcure.Domain.Sourcing;
using eProcure.Domain.Suppliers;
using eProcure.Infrastructure.Services;
using FluentAssertions;
using Xunit;

namespace eProcure.Tests;

public sealed class DashboardServiceTests
{
    private static Rfq OpenRfq(string code, DateTime now) => new()
    {
        Code = code, Title = "Pumps", Status = RfqStatus.Open,
        ClosesUtc = now.AddDays(5), CreatedUtc = now, UpdatedUtc = now,
    };

    // BACKLOG / K6a: a declined invitation is not an outstanding action and must not inflate the
    // vendor's "RFQs to bid" tile.
    [Fact]
    public async Task RFQs_to_bid_excludes_declined_invitations()
    {
        var c = TestContext.New();
        var now = c.Clock.UtcNow;
        var vendor = new Vendor { Code = "V-X", Name = "Xylo", RegisteredName = "Xylo" };
        c.Db.Vendors.Add(vendor);

        var live = OpenRfq("RFQ-2026-0801", now);
        live.Invitations.Add(RfqInvitation.Seed(live.Id, vendor.Id, RfqInvitationStatus.Invited, now));

        var declined = OpenRfq("RFQ-2026-0802", now);
        declined.Invitations.Add(RfqInvitation.Seed(declined.Id, vendor.Id, RfqInvitationStatus.Declined, now, declineReasonCode: "CAPACITY"));

        c.Db.Rfqs.AddRange(live, declined);
        await c.Db.SaveChangesAsync();

        var svc = new DashboardService(c.Db, new FakeCurrentUser("VU-x", "Xylo", ["Vendor"], vendor.Id));
        var dash = await svc.GetAsync();

        var toBid = dash.Cards.Single(card => card.Label == "RFQs to bid");
        // Only the live invitation counts — the declined RFQ is excluded.
        toBid.Value.Should().Be("1");
    }
}
