using System.Net.Http.Json;
using eProcure.Api.Auth;
using eProcure.Application.Search;
using eProcure.Domain.Identity;
using eProcure.Domain.Procurement;
using eProcure.Domain.Sourcing;
using eProcure.Domain.Suppliers;
using FluentAssertions;
using Xunit;

namespace eProcure.Tests.Integration;

/// <summary>
/// OD-4 (AUTHORIZATION-MATRIX §6): /api/search filters RESULT TYPES by the caller's permitted
/// View* actions from the ActionCatalog. The ruled case: an evaluator is denied the vendor
/// master (OD-2, alias masking end-to-end) — so an evaluator searching a vendor name must get
/// ZERO vendor-type hits; search must not undo OD-2 through the side door. Existing
/// SearchScopingTests (vendor in-query scoping) are untouched and must stay green.
/// </summary>
public sealed class RoleSearchFilterFixture : IAsyncLifetime
{
    public TestWebAppFactory Factory { get; } = new(extraDevUsers:
    [
        new DevUser("rsf_approver", "RSF Approver", "rsf_approver@test", [Roles.Approver]),
        new DevUser("rsf_tech", "RSF Tech", "rsf_tech@test", [Roles.TechEvaluator]),
    ]);

    public async Task InitializeAsync()
    {
        var now = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        await Factory.SeedAsync(db =>
        {
            // Every record type carries the same searchable token "sidedoor".
            var v = new Vendor { Code = "V-SIDEDOOR", Name = "Sidedoor Engineering", RegisteredName = "Sidedoor Engineering Sdn Bhd" };
            db.Vendors.Add(v);

            var rfq = new Rfq { Code = "RFQ-2026-7701", Title = "sidedoor cabling", ClosesUtc = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc), CreatedUtc = now, UpdatedUtc = now }.SeededAs(RfqStatus.Open);
            db.Rfqs.Add(rfq);

            db.PurchaseRequisitions.Add(new PurchaseRequisition { Code = "PR-2026-7701", Memo = "sidedoor fittings", CreatedUtc = now, UpdatedUtc = now });
            db.PurchaseOrders.Add(new PurchaseOrder { Code = "PO-2026-7701-SIDEDOOR", VendorId = v.Id, CreatedUtc = now, UpdatedUtc = now });
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

public sealed class RoleSearchFilterTests(RoleSearchFilterFixture fx) : IClassFixture<RoleSearchFilterFixture>
{
    private static async Task<List<SearchHit>> Search(HttpClient c, string q) =>
        (await c.GetFromJsonAsync<List<SearchHit>>($"/api/search?q={Uri.EscapeDataString(q)}"))!;

    [Fact]
    public async Task Evaluator_searching_a_vendor_name_gets_zero_vendor_hits()
    {
        // The operator's ruled scoping test: TechEvaluator × vendor name → no Vendor-type hits.
        var hits = await Search(fx.ClientAs("rsf_tech"), "Sidedoor");
        hits.Where(h => h.Type == "Vendor").Should().BeEmpty(
            "OD-2 denies evaluators the vendor master — search must not leak vendor names back (OD-4)");
    }

    [Fact]
    public async Task Evaluator_search_returns_only_record_types_they_may_read()
    {
        var hits = await Search(fx.ClientAs("rsf_tech"), "sidedoor");
        hits.Select(h => h.Type).Distinct().Should().BeEquivalentTo(["Rfq"],
            "an evaluator's permitted read surface is RFQs only (A8); PRs, POs, invoices and vendors are catalog-denied");
    }

    [Fact]
    public async Task Approver_search_spans_the_full_internal_read_tier()
    {
        var hits = await Search(fx.ClientAs("rsf_approver"), "sidedoor");
        // CF1-T5: Statement joined the hit classes (internal-only, ViewStatements [B,Ap,Ad]) —
        // the fixture's vendor name matches, so the Approver's tier now surfaces it too.
        hits.Select(h => h.Type).Distinct().Should().BeEquivalentTo(["Vendor", "Requisition", "Rfq", "PurchaseOrder", "Statement"],
            "OD-1 grants the Approver the full internal read tier, so every seeded type surfaces");
    }
}
