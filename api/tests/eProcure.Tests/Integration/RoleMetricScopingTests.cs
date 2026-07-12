using System.Net;
using System.Net.Http.Json;
using eProcure.Application.Dashboards;
using eProcure.Domain.Procurement;
using eProcure.Domain.Suppliers;
using FluentAssertions;
using Xunit;

namespace eProcure.Tests.Integration;

/// <summary>
/// A2F-T1 (AUTHZ-1, audit 2026-07-12 §3): the org-wide aggregate metrics leaked org totals
/// to vendor principals because they gated on ViewInvoices/ViewVendors — actions role V
/// legitimately holds for its OWN records. The twin of RoleSearchFilterTests: side doors
/// stay shut. The three spend metrics now gate on A72 ViewSpendAnalytics [B, Ap, Ad] and
/// vendorCount rides the internal-only user-directory action (ruled: reuse, not mint) —
/// a vendor is cleanly 403'd; internal users keep everything; the genuinely vendor-scoped
/// metrics keep serving each vendor its OWN numbers.
/// </summary>
public sealed class RoleMetricScopingFixture : IAsyncLifetime
{
    public TestWebAppFactory Factory { get; } = new();

    public async Task InitializeAsync()
    {
        var now = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        await Factory.SeedAsync(db =>
        {
            var hidro = new Vendor { Code = "SWK-V-11150", Name = "Hidro", RegisteredName = "Hidro Sdn Bhd" };
            var sentausa = new Vendor { Code = "SWK-V-10293", Name = "Sentausa", RegisteredName = "Sentausa Sdn Bhd" };
            db.Vendors.AddRange(hidro, sentausa);
            db.VendorUsers.AddRange(
                new VendorUser("VU-hidro", hidro.Id, "Hidro Portal", "hidro@vendor.test"),
                new VendorUser("VU-sentausa", sentausa.Id, "Sentausa Portal", "sentausa@vendor.test"));

            // Own-numbers seed: hidro has 2 issued-unacknowledged POs, sentausa 1 — the
            // vendor-scoped metrics must return each vendor ITS count, not the org's 3.
            PurchaseOrder Po(Vendor v, string code)
            {
                var po = new PurchaseOrder
                {
                    Code = code, VendorId = v.Id, CreatedUtc = now, UpdatedUtc = now,
                    Lines = { new PoLine { ItemCode = "X", Description = "x", Qty = 1, UnitPrice = 100m, Uom = "Unit" } },
                };
                po.Issue(now);
                return po;
            }
            db.PurchaseOrders.AddRange(Po(hidro, "PO-2026-8801"), Po(hidro, "PO-2026-8802"), Po(sentausa, "PO-2026-8803"));
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

public sealed class RoleMetricScopingTests(RoleMetricScopingFixture fx) : IClassFixture<RoleMetricScopingFixture>
{
    [Theory]
    [InlineData("VU-hidro")]
    [InlineData("VU-sentausa")]
    public async Task Vendors_are_403d_from_every_org_wide_aggregate(string persona)
    {
        var vendor = fx.ClientAs(persona);
        foreach (var url in new[]
        {
            "/api/metrics/committedSpendMtd/value",
            "/api/metrics/spendVsSameMonthLy/value",
            "/api/metrics/vendorCount/value",
            "/api/metrics/spendByMonth/series",
        })
        {
            (await vendor.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.Forbidden,
                $"{persona} must not read the org-wide aggregate {url} (AUTHZ-1)");
        }
    }

    [Fact]
    public async Task Internal_users_keep_the_aggregates()
    {
        var buyer = fx.ClientAs("u_faridah");
        foreach (var url in new[]
        {
            "/api/metrics/committedSpendMtd/value",
            "/api/metrics/spendVsSameMonthLy/value",
            "/api/metrics/vendorCount/value",
            "/api/metrics/spendByMonth/series",
        })
        {
            (await buyer.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.OK,
                $"the buyer must keep {url} — the fix denies vendors, never internal users");
        }
        // And the roster count is real, not a scoped zero.
        (await buyer.GetFromJsonAsync<MetricValueDto>("/api/metrics/vendorCount/value"))!.Value.Should().Be(2);
    }

    [Fact]
    public async Task Vendor_scoped_metrics_still_serve_each_vendor_its_OWN_numbers()
    {
        var hidro = await fx.ClientAs("VU-hidro").GetFromJsonAsync<MetricValueDto>("/api/metrics/vendorPosToAcknowledge/value");
        var sentausa = await fx.ClientAs("VU-sentausa").GetFromJsonAsync<MetricValueDto>("/api/metrics/vendorPosToAcknowledge/value");
        hidro!.Value.Should().Be(2, "hidro's own issued-unacknowledged POs — not the org's 3");
        sentausa!.Value.Should().Be(1);

        (await fx.ClientAs("VU-hidro").GetAsync("/api/metrics/vendorBidsSubmitted/value"))
            .StatusCode.Should().Be(HttpStatusCode.OK, "the genuinely vendor-scoped metrics are untouched");
        (await fx.ClientAs("VU-hidro").GetAsync("/api/metrics/vendorOpenPos/value"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
