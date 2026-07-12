using eProcure.Application.Dashboards;
using eProcure.Domain.Sourcing;
using eProcure.Domain.Suppliers;
using eProcure.Infrastructure.Services;
using FluentAssertions;
using Xunit;

namespace eProcure.Tests;

/// <summary>
/// The K6a semantic pinned on the METRIC LAYER (D4 Phase 4: the legacy DashboardService is
/// deleted; its per-principal semantics migrated to SystemMetricService — this file keeps
/// the original pin alive against the successor).
/// </summary>
public sealed class VendorRfqsToBidMetricTests
{
    private static Rfq OpenRfq(string code, DateTime now) => new Rfq
    {
        Code = code, Title = "Pumps",
        ClosesUtc = now.AddDays(5), CreatedUtc = now, UpdatedUtc = now,
    }.SeededAs(RfqStatus.Open);

    // BACKLOG / K6a: a declined invitation is not an outstanding action and must not inflate the
    // vendor's "RFQs to bid" number.
    [Fact]
    public async Task RFQs_to_bid_metric_excludes_declined_invitations()
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

        var metrics = new SystemMetricService(c.Db, c.Clock, new FakeCurrentUser("VU-x", "Xylo", ["Vendor"], vendor.Id));
        var m = await metrics.ValueAsync(MetricIds.VendorRfqsToBid);

        // Only the live invitation counts — the declined RFQ is excluded.
        m.Value.Should().Be(1);
    }
}
