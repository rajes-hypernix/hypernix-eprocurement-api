using System.Net;
using System.Net.Http.Json;
using eProcure.Application.Dashboards;
using eProcure.Application.Views;
using eProcure.Domain.Procurement;
using eProcure.Domain.Sourcing;
using eProcure.Domain.Suppliers;
using FluentAssertions;
using Xunit;

namespace eProcure.Tests.Integration;

/// <summary>
/// D4 Phase 2. The load-bearing pins: (1) PARITY — the metric layer reproduces the retiring
/// DashboardService byte-identically, asserted by comparing the legacy endpoint's cards
/// against the metrics BEFORE A1 dies; (2) the SoD/scoping semantics that migrate with the
/// per-principal metrics (Approver creator≠me, evaluator assigned-to-me, vendor own-scope)
/// — the A1-retirement condition; (3) honest-null (prToPoCycleDays on backfill-null
/// IssuedUtc; vs-LY without history renders "not yet available"); (4) aggregation
/// correctness incl. all-null → null + ExcludedNullCount and series unbucketedCount.
/// </summary>
public sealed class DashboardMetricsFixture : IAsyncLifetime
{
    public TestWebAppFactory Factory { get; } = new();
    public Guid RfqOpenInvitedA { get; private set; }

    public async Task InitializeAsync()
    {
        var now = DateTime.UtcNow;
        var thisMonth15 = new DateTime(now.Year, now.Month, 15, 10, 0, 0, DateTimeKind.Utc);
        var lastMonth = thisMonth15.AddMonths(-1);
        var lyDate = DateOnly.FromDateTime(thisMonth15.AddMonths(-12));
        var mtdDate = DateOnly.FromDateTime(thisMonth15);

        await Factory.SeedAsync(db =>
        {
            var a = new Vendor { Code = "V-D4A", Name = "D4 Alpha", RegisteredName = "D4 Alpha Sdn Bhd", Type = VendorType.Swec, CreatedUtc = thisMonth15, UpdatedUtc = now };
            var b = new Vendor { Code = "V-D4B", Name = "D4 Beta", RegisteredName = "D4 Beta Sdn Bhd", Type = VendorType.NonSwec, CreatedUtc = lastMonth, UpdatedUtc = now };
            db.Vendors.AddRange(a, b);
            db.VendorUsers.Add(new VendorUser("VU-D4A", a.Id, "Alpha User", "d4a@vendor.test"));

            // RFQs: one OPEN invited to A; one CLOSED assigned to u_hafiz (tech pending);
            // one EVALUATION TechFinalized assigned to u_tan (comm pending); one DRAFT with null ClosesUtc.
            var open = new Rfq { Code = "RFQ-2026-7401", Title = "D4 open", ClosesUtc = thisMonth15.AddDays(5), CreatedUtc = now, UpdatedUtc = now }.SeededAs(RfqStatus.Open);
            open.Invitations.Add(RfqInvitation.Seed(open.Id, a.Id, RfqInvitationStatus.Invited, now));
            var closed = new Rfq { Code = "RFQ-2026-7402", Title = "D4 closed", ClosesUtc = thisMonth15.AddDays(-2), CreatedUtc = now, UpdatedUtc = now, TechnicalEvaluatorIds = ["u_hafiz"] }.SeededAs(RfqStatus.Closed);
            var eval = new Rfq
            {
                Code = "RFQ-2026-7403", Title = "D4 eval", ClosesUtc = thisMonth15.AddDays(-9),
                CreatedUtc = now, UpdatedUtc = now, CommercialEvaluatorIds = ["u_tan"],
                TechFinalized = true, CommercialOpened = false,
            }.SeededAs(RfqStatus.Evaluation);
            var draft = new Rfq { Code = "RFQ-2026-7404", Title = "D4 draft", ClosesUtc = null, CreatedUtc = now, UpdatedUtc = now }.SeededAs(RfqStatus.Draft);
            db.Rfqs.AddRange(open, closed, eval, draft);
            RfqOpenInvitedA = open.Id;

            // Awards: PendingApproval by u_faridah (u_lim MUST count it) and by u_lim (u_lim must NOT — SoD).
            db.Awards.AddRange(
                new Award { Code = "AWD-7401", RfqId = eval.Id, CreatedByUserId = "u_faridah", CreatedUtc = now, UpdatedUtc = now },
                new Award { Code = "AWD-7402", RfqId = open.Id, CreatedByUserId = "u_lim", CreatedUtc = now, UpdatedUtc = now });

            // POs: none issued (IssuedUtc null — the honest-null pin for prToPoCycleDays).
            db.PurchaseOrders.Add(new PurchaseOrder { Code = "PO-2026-7401", VendorId = a.Id, CreatedUtc = now, UpdatedUtc = now });

            // Invoices: two this month (700 + 300) and one same month LAST YEAR (500) → vs-LY = +100%.
            Invoice Inv(string code, DateOnly date, decimal qty, decimal price) => new()
            {
                Code = code, PoId = Guid.NewGuid(), VendorId = a.Id, InvoiceNo = code, Date = date,
                CreatedUtc = now, UpdatedUtc = now,
                Lines = { new InvoiceLine { ItemCode = "X", Qty = qty, UnitPrice = price } },
            };
            db.Invoices.AddRange(
                Inv("INV-7401", mtdDate, 1, 700m),
                Inv("INV-7402", mtdDate, 1, 300m),
                Inv("INV-7403", lyDate, 1, 500m));

            db.FieldRegistry.AddRange(FieldRegistrySeed.ToEntities());
            foreach (var row in DashboardSeed.Rows)
            {
                var dash = new Domain.Dashboards.Dashboard { Code = row.Code, Name = row.Name, OwnerRole = row.OwnerRole, IsRoleDefault = true, CreatedUtc = now, UpdatedUtc = now };
                dash.Portlets.AddRange(row.Portlets.Select(p => new Domain.Dashboards.PortletInstance
                {
                    DashboardId = dash.Id, PortletType = p.Type, Title = p.Title,
                    Col = p.Col, Row = p.Row, Width = p.Width, SavedViewId = p.SavedViewId, ConfigJson = p.ConfigJson,
                }));
                db.Dashboards.Add(dash);
            }
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

    public async Task<MetricValueDto> Metric(string persona, string id)
    {
        var resp = await ClientAs(persona).GetAsync($"/api/metrics/{id}/value");
        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        return (await resp.Content.ReadFromJsonAsync<MetricValueDto>())!;
    }
}

public sealed class DashboardMetricsTests(DashboardMetricsFixture fx) : IClassFixture<DashboardMetricsFixture>
{
    // ---- (1) PARITY: pinned against the legacy /api/dashboard cards BEFORE A1 retired
    // (CI-recorded at d4 phase 2, run 29152084168-era). The legacy endpoint is now deleted
    // (sanctioned A1 retirement), so the same values are pinned explicitly from the fixture:
    // the queries are byte-identical by construction (OD-D4-1: openRequisitions counts ALL PRs). ----

    [Fact]
    public async Task Migrated_stat_card_metrics_hold_their_pre_retirement_values()
    {
        (await fx.Metric("u_faridah", MetricIds.OpenRequisitions)).Value.Should().Be(0, "the fixture seeds no PRs (count-ALL query per OD-D4-1)");
        (await fx.Metric("u_faridah", MetricIds.RfqsAwaitingBids)).Value.Should().Be(1, "one Open RFQ");
        (await fx.Metric("u_faridah", MetricIds.RfqsReadyToOpen)).Value.Should().Be(1, "one Closed RFQ");
        (await fx.Metric("u_faridah", MetricIds.RfqsUnderEvaluation)).Value.Should().Be(1, "one Evaluation RFQ");
    }

    // ---- (2) the migrated SoD / scoping semantics (the A1-retirement condition) ----

    [Fact]
    public async Task Approver_metric_never_counts_awards_the_caller_created()
    {
        (await fx.Metric("u_lim", MetricIds.AwardsToApprove)).Value.Should().Be(1,
            "u_lim must count u_faridah's pending award but NOT its own (SoD creator≠me)");
    }

    [Fact]
    public async Task Evaluator_metrics_count_only_rfqs_assigned_to_the_caller()
    {
        (await fx.Metric("u_hafiz", MetricIds.TechScoringPending)).Value.Should().Be(1, "assigned to the closed RFQ");
        (await fx.Metric("u_nur", MetricIds.TechScoringPending)).Value.Should().Be(0, "u_nur is a TechEvaluator but not assigned");
        (await fx.Metric("u_tan", MetricIds.CommOpeningsPending)).Value.Should().Be(1, "tech finalized, commercial unopened, assigned");
    }

    [Fact]
    public async Task Vendor_metrics_are_vendor_scoped_and_internal_probes_are_403()
    {
        (await fx.Metric("VU-D4A", MetricIds.VendorRfqsToBid)).Value.Should().Be(1);
        (await fx.ClientAs("u_faridah").GetAsync($"/api/metrics/{MetricIds.VendorRfqsToBid}/value"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "vendor metrics ride ViewMyInvitations — vendor principals only");
    }

    [Fact]
    public async Task Per_metric_required_action_gates_direct_probes()
    {
        (await fx.ClientAs("u_hafiz").GetAsync($"/api/metrics/{MetricIds.CommittedSpendMtd}/value"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "an evaluator is denied ViewInvoices (OD-2) — the metric layer must not leak it");
    }

    // ---- (3) honest-null ----

    [Fact]
    public async Task PrToPoCycle_is_not_yet_available_while_issuedutc_is_backfill_null()
    {
        var m = await fx.Metric("u_faridah", MetricIds.PrToPoCycleDays);
        m.NotYetAvailable.Should().BeTrue("no seeded PO carries IssuedUtc — never render a fabricated zero");
        m.Value.Should().BeNull();
    }

    [Fact]
    public async Task Spend_metrics_compute_from_real_invoice_history()
    {
        (await fx.Metric("u_faridah", MetricIds.CommittedSpendMtd)).Value.Should().Be(1000m, "700 + 300 this month");
        var vsLy = await fx.Metric("u_faridah", MetricIds.SpendVsSameMonthLy);
        vsLy.NotYetAvailable.Should().BeFalse("the fixture seeds a same-month-last-year invoice");
        vsLy.Value.Should().Be(100m, "1000 vs 500 = +100%");
    }

    [Fact]
    public async Task Series_buckets_align_to_the_clock_and_count_honestly()
    {
        var resp = await fx.ClientAs("u_faridah").GetFromJsonAsync<MetricSeriesDto>($"/api/metrics/{MetricIds.SpendByMonth}/series?months=13");
        var thisMonth = DateTime.UtcNow.ToString("yyyy-MM");
        resp!.Buckets.Single(b => b.Bucket == thisMonth).Value.Should().Be(1000m);
        resp.Buckets.First().Value.Should().Be(500m, "the LY invoice lands in the first of 13 buckets");

        var swec = await fx.ClientAs("u_faridah").GetFromJsonAsync<MetricSeriesDto>($"/api/metrics/{MetricIds.VendorsOnboardedSwecByMonth}/series?months=12");
        swec!.Buckets.Single(b => b.Bucket == thisMonth).Value.Should().Be(1, "vendor A (SWEC) was created this month");
    }

    // ---- (4) view aggregation (rides the D3 pipeline) ----

    private async Task<SavedViewDto> CreateRfqView(HttpClient c)
    {
        var resp = await c.PostAsJsonAsync("/api/views", new SaveViewRequest("d4-agg", "Rfq", [],
            [new SavedViewColumnDto("Code", null, null), new SavedViewColumnDto("InvitedCount", null, null)]));
        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        return (await resp.Content.ReadFromJsonAsync<SavedViewDto>())!;
    }

    [Fact]
    public async Task Aggregate_count_sum_avg_over_the_scoped_pipeline()
    {
        var buyer = fx.ClientAs("u_faridah");
        var view = await CreateRfqView(buyer);
        (await buyer.GetFromJsonAsync<ViewAggregateResult>($"/api/views/{view.Id}/aggregate?fn=count"))!
            .Value.Should().Be(4, "four seeded RFQs");
        (await buyer.GetFromJsonAsync<ViewAggregateResult>($"/api/views/{view.Id}/aggregate?fn=sum&field=InvitedCount"))!
            .Value.Should().Be(1, "one invitation across all RFQs");
        (await buyer.GetFromJsonAsync<ViewAggregateResult>($"/api/views/{view.Id}/aggregate?fn=avg&field=InvitedCount"))!
            .Value.Should().Be(0.25m);
        (await buyer.GetAsync($"/api/views/{view.Id}/aggregate?fn=sum&field=Title"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "sum needs a Money/Number field");
    }

    [Fact]
    public async Task Aggregate_over_all_null_inputs_is_null_with_the_count_surfaced()
    {
        var admin = fx.ClientAs("u_admin");
        var resp = await admin.PostAsJsonAsync("/api/views", new SaveViewRequest("d4-otd", "Vendor", [],
            [new SavedViewColumnDto("Code", null, null), new SavedViewColumnDto("Otd", null, null)]));
        var view = (await resp.Content.ReadFromJsonAsync<SavedViewDto>())!;
        var agg = await admin.GetFromJsonAsync<ViewAggregateResult>($"/api/views/{view.Id}/aggregate?fn=avg&field=Otd");
        agg!.Value.Should().BeNull("every vendor's Otd is honest-null (no promised-delivery date)");
        agg.ExcludedNullCount.Should().Be(2, "both vendors' null Otd are surfaced, not silently averaged as zero");
    }

    [Fact]
    public async Task Vendor_aggregate_is_scoped_to_its_own_reachable_rows()
    {
        var buyer = fx.ClientAs("u_faridah");
        var view = await CreateRfqView(buyer);
        (await buyer.PostAsJsonAsync($"/api/views/{view.Id}/share", new ShareViewRequest(true))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await fx.ClientAs("VU-D4A").GetFromJsonAsync<ViewAggregateResult>($"/api/views/{view.Id}/aggregate?fn=count"))!
            .Value.Should().Be(1, "the D3 scoping proof extended to numbers: vendor counts only its invited RFQ");
    }

    [Fact]
    public async Task View_series_buckets_by_month_and_surfaces_unbucketed_rows()
    {
        var buyer = fx.ClientAs("u_faridah");
        var view = await CreateRfqView(buyer);
        var s = await buyer.GetFromJsonAsync<ViewSeriesResult>($"/api/views/{view.Id}/series?fn=count&bucket=ClosesUtc&months=3");
        s!.UnbucketedCount.Should().Be(1, "the draft RFQ has no ClosesUtc — excluded and surfaced, never silently dropped");
        s.Buckets.Sum(b => b.Value).Should().Be(3);
    }

    // ---- dashboards store ----

    [Fact]
    public async Task Multi_role_mine_is_the_deduplicated_union_and_personalize_is_copy_on_write()
    {
        var lim = fx.ClientAs("u_lim");
        var mine = await lim.GetFromJsonAsync<UserDashboardDto>("/api/dashboards/mine");
        mine!.IsPersonalized.Should().BeFalse();
        mine.Portlets.Select(p => p.Title).Should().Contain(["Sourcing pipeline", "Awards to approve"],
            "Buyer + Approver defaults union — reproduces the legacy role-branch merge");

        var copy = await (await lim.PostAsync("/api/dashboards/personalize", null)).Content.ReadFromJsonAsync<UserDashboardDto>();
        copy!.IsPersonalized.Should().BeTrue();
        copy.Portlets.Count.Should().Be(mine.Portlets.Count);

        // Arrange: keep only the approver meter, full width, row 0.
        var meter = copy.Portlets.First(p => p.Title == "Awards to approve");
        var upd = await lim.PutAsJsonAsync("/api/dashboards/mine", new UpdateDashboardRequest(null,
            [new PortletUpsert(meter.Id, meter.PortletType, meter.Title, 0, 0, 2, meter.SavedViewId, meter.ConfigJson)]));
        upd.StatusCode.Should().Be(HttpStatusCode.OK, await upd.Content.ReadAsStringAsync());
        (await lim.GetFromJsonAsync<UserDashboardDto>("/api/dashboards/mine"))!.Portlets.Should().HaveCount(1);

        // Reset falls back to the role-default union; role defaults were never mutated.
        (await lim.DeleteAsync("/api/dashboards/mine")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await lim.GetFromJsonAsync<UserDashboardDto>("/api/dashboards/mine"))!.Portlets.Count.Should().Be(mine.Portlets.Count);
    }

    [Fact]
    public async Task Invalid_portlet_config_is_400_and_role_defaults_are_admin_only()
    {
        var lim = fx.ClientAs("u_lim");
        await lim.PostAsync("/api/dashboards/personalize", null);
        (await lim.PutAsJsonAsync("/api/dashboards/mine", new UpdateDashboardRequest(null,
            [new PortletUpsert(null, "KpiMeter", "bad", 0, 0, 1, null, "{}")])))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "a KpiMeter with neither metric nor view is invalid");
        await lim.DeleteAsync("/api/dashboards/mine");

        (await fx.ClientAs("u_admin").PutAsJsonAsync("/api/dashboards/role-defaults/NoSuchRole", new UpdateDashboardRequest(null,
            [new PortletUpsert(null, "RecentRecords", "x", 0, 0, 1, null, "{}")])))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "unknown role");
    }
}
