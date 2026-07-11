using System.Net;
using eProcure.Domain.Procurement;
using eProcure.Domain.Sourcing;
using eProcure.Domain.Suppliers;
using FluentAssertions;
using Xunit;

namespace eProcure.Tests.Integration;

/// <summary>
/// RM-P1 (the Slice F patch, AUTHORIZATION-MATRIX Obs-1 + Obs-4): vendor resource scoping on the
/// two reads the role gate alone could not close — RFQ detail and GRN-for-ASN. Same convention as
/// VendorScopingTests: a foreign vendor's probe is a 403 (ForbiddenException / EnsureCanAccess),
/// never a data leak; internal reviewers are unrestricted; the live-invitation rule mirrors the
/// RFQ list's (Rescinded rows are not live).
/// </summary>
public sealed class RfqGrnScopingFixture : IAsyncLifetime
{
    public TestWebAppFactory Factory { get; } = new();
    public Guid RfqInvitedId { get; private set; }
    public Guid RfqForeignId { get; private set; }
    public Guid RfqRescindedId { get; private set; }
    public Guid AsnAId { get; private set; }

    public async Task InitializeAsync()
    {
        var now = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        await Factory.SeedAsync(db =>
        {
            var a = new Vendor { Code = "V-RS-A", Name = "ScopeAlpha", RegisteredName = "ScopeAlpha Sdn Bhd" };
            var b = new Vendor { Code = "V-RS-B", Name = "ScopeBeta", RegisteredName = "ScopeBeta Sdn Bhd" };
            db.Vendors.AddRange(a, b);
            db.VendorUsers.AddRange(
                new VendorUser("VU-RS-A", a.Id, "Alpha User", "rsa@vendor.test"),
                new VendorUser("VU-RS-B", b.Id, "Beta User", "rsb@vendor.test"));

            var closes = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var invited = new Rfq { Code = "RFQ-2026-9101", Title = "Invited-to-A", ClosesUtc = closes, CreatedUtc = now, UpdatedUtc = now }.SeededAs(RfqStatus.Open);
            invited.Invitations.Add(RfqInvitation.Seed(invited.Id, a.Id, RfqInvitationStatus.Invited, now));
            var foreign = new Rfq { Code = "RFQ-2026-9102", Title = "Invited-to-B-only", ClosesUtc = closes, CreatedUtc = now, UpdatedUtc = now }.SeededAs(RfqStatus.Open);
            foreign.Invitations.Add(RfqInvitation.Seed(foreign.Id, b.Id, RfqInvitationStatus.Invited, now));
            var rescinded = new Rfq { Code = "RFQ-2026-9103", Title = "A-was-rescinded", ClosesUtc = closes, CreatedUtc = now, UpdatedUtc = now }.SeededAs(RfqStatus.Open);
            rescinded.Invitations.Add(RfqInvitation.Seed(rescinded.Id, a.Id, RfqInvitationStatus.Rescinded, now));
            db.Rfqs.AddRange(invited, foreign, rescinded);

            var po = new PurchaseOrder { Code = "PO-2026-9101", VendorId = a.Id, CreatedUtc = now, UpdatedUtc = now };
            db.PurchaseOrders.Add(po);
            var asn = new Asn { Code = "ASN-2026-9101", PoId = po.Id, VendorId = a.Id, CreatedUtc = now, UpdatedUtc = now };
            db.Asns.Add(asn);
            db.Grns.Add(new Grn { Code = "GRN-2026-9101", AsnId = asn.Id, PoId = po.Id, CreatedUtc = now });

            RfqInvitedId = invited.Id;
            RfqForeignId = foreign.Id;
            RfqRescindedId = rescinded.Id;
            AsnAId = asn.Id;
            return Task.CompletedTask;
        });
    }

    public Task DisposeAsync() { Factory.Dispose(); return Task.CompletedTask; }

    public HttpClient ClientAs(string demoUser)
    {
        var client = Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Demo-User", demoUser);
        return client;
    }
}

public sealed class RfqGrnScopingTests(RfqGrnScopingFixture fx) : IClassFixture<RfqGrnScopingFixture>
{
    [Fact]
    public async Task Vendor_reads_rfq_detail_only_with_a_live_invitation()
    {
        (await fx.ClientAs("VU-RS-A").GetAsync($"/api/rfqs/{fx.RfqInvitedId}"))
            .StatusCode.Should().Be(HttpStatusCode.OK, "vendor A holds a live invitation");

        (await fx.ClientAs("VU-RS-A").GetAsync($"/api/rfqs/{fx.RfqForeignId}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "vendor A was never invited — the detail must not leak");

        (await fx.ClientAs("VU-RS-A").GetAsync($"/api/rfqs/{fx.RfqRescindedId}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "a rescinded invitation is not live (same rule as the list)");
    }

    [Fact]
    public async Task Internal_reviewers_read_any_rfq_detail()
    {
        (await fx.ClientAs("u_faridah").GetAsync($"/api/rfqs/{fx.RfqForeignId}"))
            .StatusCode.Should().Be(HttpStatusCode.OK, "internal users are unrestricted by vendor scoping");
    }

    [Fact]
    public async Task Grn_read_inherits_the_asn_vendor_chain()
    {
        (await fx.ClientAs("VU-RS-A").GetAsync($"/api/asns/{fx.AsnAId}/grn"))
            .StatusCode.Should().Be(HttpStatusCode.OK, "vendor A owns the ASN/PO chain");

        (await fx.ClientAs("VU-RS-B").GetAsync($"/api/asns/{fx.AsnAId}/grn"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "vendor B must not read another vendor's receipt");

        (await fx.ClientAs("u_faridah").GetAsync($"/api/asns/{fx.AsnAId}/grn"))
            .StatusCode.Should().Be(HttpStatusCode.OK, "internal reviewers read any GRN");
    }
}
