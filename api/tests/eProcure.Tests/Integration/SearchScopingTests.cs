using System.Net.Http.Json;
using eProcure.Application.Search;
using eProcure.Domain.Procurement;
using eProcure.Domain.Sourcing;
using eProcure.Domain.Suppliers;
using FluentAssertions;
using Xunit;

namespace eProcure.Tests.Integration;

/// <summary>
/// Global search (D2): two vendors with look-alike codes prove the perimeter.
/// Operator condition: scoping is IN the query — a vendor searching a string
/// that matches ANOTHER vendor's PO code gets zero hits, not a late filter.
/// </summary>
public sealed class SearchScopingFixture : IAsyncLifetime
{
    public TestWebAppFactory Factory { get; } = new();
    public Guid VendorAId { get; private set; }
    public Guid VendorBId { get; private set; }

    public async Task InitializeAsync()
    {
        var now = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        await Factory.SeedAsync(db =>
        {
            var a = new Vendor { Code = "V-SRCH-A", Name = "SearchAlpha", RegisteredName = "SearchAlpha Sdn Bhd" };
            var b = new Vendor { Code = "V-SRCH-B", Name = "SearchBeta", RegisteredName = "SearchBeta Sdn Bhd" };
            db.Vendors.AddRange(a, b);
            db.VendorUsers.AddRange(
                new VendorUser("VU-SRCH-A", a.Id, "Alpha User", "sa@vendor.test"),
                new VendorUser("VU-SRCH-B", b.Id, "Beta User", "sb@vendor.test"));

            var rfqA = new Rfq { Code = "RFQ-2026-8801", Title = "Search cables", ClosesUtc = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc), CreatedUtc = now, UpdatedUtc = now }.SeededAs(RfqStatus.Open);
            rfqA.Invitations.Add(RfqInvitation.Seed(rfqA.Id, a.Id, RfqInvitationStatus.Invited, now));
            var rfqB = new Rfq { Code = "RFQ-2026-8802", Title = "Search valves", ClosesUtc = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc), CreatedUtc = now, UpdatedUtc = now }.SeededAs(RfqStatus.Open);
            rfqB.Invitations.Add(RfqInvitation.Seed(rfqB.Id, b.Id, RfqInvitationStatus.Invited, now));
            db.Rfqs.AddRange(rfqA, rfqB);

            // The nasty case: B's PO code is a string A might well type.
            db.PurchaseOrders.AddRange(
                new PurchaseOrder { Code = "PO-2026-8801", VendorId = a.Id, CreatedUtc = now, UpdatedUtc = now },
                new PurchaseOrder { Code = "PO-2026-8802", VendorId = b.Id, CreatedUtc = now, UpdatedUtc = now });

            db.PurchaseRequisitions.Add(new PurchaseRequisition { Code = "PR-2026-8801", Memo = "search fittings", CreatedUtc = now, UpdatedUtc = now });

            VendorAId = a.Id;
            VendorBId = b.Id;
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

public sealed class SearchScopingTests(SearchScopingFixture fx) : IClassFixture<SearchScopingFixture>
{
    private static async Task<List<SearchHit>> Search(HttpClient c, string q) =>
        (await c.GetFromJsonAsync<List<SearchHit>>($"/api/search?q={Uri.EscapeDataString(q)}"))!;

    [Fact]
    public async Task Buyer_search_spans_all_record_types()
    {
        var hits = await Search(fx.ClientAs("u_faridah"), "8801");
        hits.Select(h => h.Type).Should().Contain(["Rfq", "PurchaseOrder", "Requisition"]);
        hits.Should().Contain(h => h.Type == "Rfq" && h.Code == "RFQ-2026-8801");
        hits.Should().Contain(h => h.Type == "Requisition" && h.Code == "PR-2026-8801");
    }

    [Fact]
    public async Task Vendor_searching_another_vendors_po_code_gets_ZERO_hits()
    {
        // Operator condition: the cross-vendor code match. PO-2026-8802 is B's.
        var hits = await Search(fx.ClientAs("VU-SRCH-A"), "PO-2026-8802");
        hits.Should().BeEmpty("vendor A must not learn that another vendor's PO code exists");
    }

    [Fact]
    public async Task Vendor_sees_only_own_reachable_records()
    {
        var hits = await Search(fx.ClientAs("VU-SRCH-A"), "8801");
        hits.Should().NotBeEmpty();
        hits.Should().OnlyContain(h => h.Type == "Rfq" || h.Type == "PurchaseOrder" || h.Type == "Invoice" || h.Type == "Vendor");
        hits.Should().Contain(h => h.Type == "PurchaseOrder" && h.Code == "PO-2026-8801");
        hits.Should().NotContain(h => h.Type == "Requisition");   // PRs are buyer-side, never vendor-visible
    }

    [Fact]
    public async Task Vendor_rfq_hits_respect_the_live_invitation_rule()
    {
        var hits = await Search(fx.ClientAs("VU-SRCH-B"), "RFQ-2026-88");
        hits.Where(h => h.Type == "Rfq").Should().OnlyContain(h => h.Code == "RFQ-2026-8802");
    }

    [Fact]
    public async Task Vendor_finding_vendors_finds_only_itself()
    {
        var hits = await Search(fx.ClientAs("VU-SRCH-A"), "Search");
        hits.Where(h => h.Type == "Vendor").Should().OnlyContain(h => h.Id == fx.VendorAId)
            .And.NotContain(h => h.Id == fx.VendorBId);
    }

    [Fact]
    public async Task Short_queries_return_nothing()
    {
        (await Search(fx.ClientAs("u_faridah"), "8")).Should().BeEmpty();
    }
}
