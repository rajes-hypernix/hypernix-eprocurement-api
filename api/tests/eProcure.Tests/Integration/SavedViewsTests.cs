using System.Net;
using System.Net.Http.Json;
using eProcure.Application.Views;
using eProcure.Domain.Sourcing;
using eProcure.Domain.Suppliers;
using eProcure.Domain.Views;
using FluentAssertions;
using Xunit;

namespace eProcure.Tests.Integration;

/// <summary>
/// D3 Phase 2: the executor + API against the live host. Pins the ruled behaviours:
/// operator correctness per DataType (incl. the relative-date tokens), the scoping matrix
/// (a vendor running a SHARED view sees only its invited RFQs; a TechEvaluator touching a
/// Vendor-type view gets 403 via the record type's View* check), registry validation
/// (unknown FieldKey → 400 on save, loud 400 on run), and ownership/sharing rules.
/// </summary>
public sealed class SavedViewsFixture : IAsyncLifetime
{
    public TestWebAppFactory Factory { get; } = new();
    public Guid RfqInvitedToA { get; private set; }
    public Guid SystemViewId { get; private set; }
    public Guid DeadKeyViewId { get; private set; }
    public DateTime MidMonth { get; private set; }

    public async Task InitializeAsync()
    {
        var now = DateTime.UtcNow;
        // Anchor in the middle of the CURRENT month so @startOfMonth/@endOfMonth tests are
        // stable on any run date; the "far" RFQ closes two months out.
        MidMonth = new DateTime(now.Year, now.Month, 15, 12, 0, 0, DateTimeKind.Utc);
        var far = MidMonth.AddMonths(2);

        await Factory.SeedAsync(db =>
        {
            var a = new Vendor { Code = "V-D3A", Name = "D3 Alpha", RegisteredName = "D3 Alpha Sdn Bhd" };
            var b = new Vendor { Code = "V-D3B", Name = "D3 Beta", RegisteredName = "D3 Beta Sdn Bhd" };
            db.Vendors.AddRange(a, b);
            db.VendorUsers.AddRange(
                new VendorUser("VU-D3A", a.Id, "Alpha User", "d3a@vendor.test"),
                new VendorUser("VU-D3B", b.Id, "Beta User", "d3b@vendor.test"));

            var r1 = new Rfq { Code = "RFQ-2026-7301", Title = "Alpha cables", ClosesUtc = MidMonth, CreatedUtc = now, UpdatedUtc = now }.SeededAs(RfqStatus.Open);
            r1.Invitations.Add(RfqInvitation.Seed(r1.Id, a.Id, RfqInvitationStatus.Invited, now));
            var r2 = new Rfq { Code = "RFQ-2026-7302", Title = "Beta valves", ClosesUtc = far, CreatedUtc = now, UpdatedUtc = now }.SeededAs(RfqStatus.Open);
            r2.Invitations.Add(RfqInvitation.Seed(r2.Id, b.Id, RfqInvitationStatus.Invited, now));
            var r3 = new Rfq { Code = "RFQ-2026-7303", Title = "Gamma pipes", ClosesUtc = MidMonth.AddDays(-3), CreatedUtc = now, UpdatedUtc = now }.SeededAs(RfqStatus.Closed);
            db.Rfqs.AddRange(r1, r2, r3);
            RfqInvitedToA = r1.Id;

            // Registry from THE single source (mirrors the migration seed on real Postgres).
            db.FieldRegistry.AddRange(FieldRegistrySeed.ToEntities());

            // System view — mirrors the migration's "All RFQs".
            var sys = new SavedView
            {
                Code = "VIEW-SYS-0001", Name = "All RFQs", RecordType = RecordType.Rfq,
                OwnerUserId = null, IsShared = true, IsSystem = true, CreatedUtc = now, UpdatedUtc = now,
            };
            foreach (var (key, i) in new[] { "Code", "Title", "Envelope", "InvitedCount", "BidCount", "ClosesUtc", "Status" }.Select((k, i) => (k, i)))
                sys.Columns.Add(new SavedViewColumn { SavedViewId = sys.Id, FieldKey = key, Sort = i });
            db.SavedViews.Add(sys);
            SystemViewId = sys.Id;

            // A view whose filter references a key the registry does not know — seeded directly
            // (the API would refuse it) to prove the run fails LOUDLY, not by dropping the filter.
            var dead = new SavedView
            {
                Code = "VIEW-DEAD-0001", Name = "Dead key", RecordType = RecordType.Rfq,
                OwnerUserId = "u_faridah", IsShared = true, IsSystem = false, CreatedUtc = now, UpdatedUtc = now,
            };
            dead.Filters.Add(new SavedViewFilter { SavedViewId = dead.Id, FieldKey = "NoSuchField", Operator = ViewOperator.Eq, Value = "x", Sort = 0 });
            dead.Columns.Add(new SavedViewColumn { SavedViewId = dead.Id, FieldKey = "Code", Sort = 0 });
            db.SavedViews.Add(dead);
            DeadKeyViewId = dead.Id;
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

public sealed class SavedViewsTests(SavedViewsFixture fx) : IClassFixture<SavedViewsFixture>
{
    private static SaveViewRequest Rfq(string name, List<SavedViewFilterDto> filters) =>
        new(name, "Rfq", filters, [new("Code", null, null), new("Title", null, null), new("Status", null, null), new("ClosesUtc", null, null)]);

    private async Task<SavedViewDto> Create(HttpClient c, SaveViewRequest req)
    {
        var resp = await c.PostAsJsonAsync("/api/views", req);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        return (await resp.Content.ReadFromJsonAsync<SavedViewDto>())!;
    }

    private async Task<ViewRunResult> Run(HttpClient c, Guid id)
    {
        var resp = await c.GetAsync($"/api/views/{id}/run");
        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        return (await resp.Content.ReadFromJsonAsync<ViewRunResult>())!;
    }

    private static IEnumerable<string?> Codes(ViewRunResult r) => r.Rows.Select(row => row["Code"]?.ToString());

    // ---- operators ----

    [Fact]
    public async Task Eq_and_membership_filter_by_status()
    {
        var buyer = fx.ClientAs("u_faridah");
        var open = await Create(buyer, Rfq("d3-open", [new("Status", "Eq", "Open", null)]));
        Codes(await Run(buyer, open.Id)).Should().BeEquivalentTo(["RFQ-2026-7301", "RFQ-2026-7302"]);

        var members = await Create(buyer, Rfq("d3-open-or-closed",
            [new("Status", "In", "Open", null), new("Status", "In", "Closed", null)]));
        Codes(await Run(buyer, members.Id)).Should().BeEquivalentTo(["RFQ-2026-7301", "RFQ-2026-7302", "RFQ-2026-7303"]);
    }

    [Fact]
    public async Task Contains_matches_title_case_insensitively()
    {
        var buyer = fx.ClientAs("u_faridah");
        var view = await Create(buyer, Rfq("d3-contains", [new("Title", "Contains", "CABLE", null)]));
        Codes(await Run(buyer, view.Id)).Should().BeEquivalentTo(["RFQ-2026-7301"]);
    }

    [Fact]
    public async Task Between_with_relative_month_tokens_is_the_gate_criterion()
    {
        var buyer = fx.ClientAs("u_faridah");
        // "Open RFQs closing this month" — the ruled gate scenario, saved with tokens so it
        // stays correct across month boundaries.
        var view = await Create(buyer, Rfq("d3-closing-this-month",
            [new("Status", "Eq", "Open", null), new("ClosesUtc", "Between", "@startOfMonth", "@endOfMonth")]));
        Codes(await Run(buyer, view.Id)).Should().BeEquivalentTo(["RFQ-2026-7301"],
            "the far RFQ closes in two months and the closed one is not Open");
    }

    [Fact]
    public async Task Gte_on_number_and_sorted_column_shape_the_result()
    {
        var buyer = fx.ClientAs("u_faridah");
        var resp = await buyer.PostAsJsonAsync("/api/views", new SaveViewRequest(
            "d3-invited", "Rfq",
            [new("InvitedCount", "Gte", "1", null)],
            [new("Code", null, "Desc"), new("Title", null, null)]));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var view = (await resp.Content.ReadFromJsonAsync<SavedViewDto>())!;
        var run = await Run(buyer, view.Id);
        Codes(run).Should().ContainInOrder("RFQ-2026-7302", "RFQ-2026-7301");   // Desc sort on Code
        run.Rows.Should().OnlyContain(r => r.ContainsKey("Id"), "rows carry the implicit Id");
    }

    // ---- the scoping matrix ----

    [Fact]
    public async Task Vendor_running_a_shared_view_sees_only_its_invited_rfqs()
    {
        var buyer = fx.ClientAs("u_faridah");
        var view = await Create(buyer, Rfq("d3-shared-open", [new("Status", "Eq", "Open", null)]));
        (await buyer.PostAsJsonAsync($"/api/views/{view.Id}/share", new ShareViewRequest(true)))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var run = await Run(fx.ClientAs("VU-D3A"), view.Id);
        Codes(run).Should().BeEquivalentTo(["RFQ-2026-7301"],
            "the executor rides the invitation-scoped RFQ source — vendor A must not see B's RFQ");
    }

    [Fact]
    public async Task TechEvaluator_touching_a_vendor_type_view_gets_403()
    {
        var admin = fx.ClientAs("u_admin");
        var vendorView = await Create(admin, new SaveViewRequest("d3-vendors", "Vendor", [], [new("Code", null, null), new("Name", null, null)]));
        (await admin.PostAsJsonAsync($"/api/views/{vendorView.Id}/share", new ShareViewRequest(true)))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var tech = fx.ClientAs("u_hafiz");
        (await tech.GetAsync($"/api/views/{vendorView.Id}/run"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "OD-2 denies evaluators the vendor master; the run's View* check enforces it");
        (await tech.PostAsJsonAsync("/api/views", new SaveViewRequest("d3-tech-vendors", "Vendor", [], [new("Code", null, null)])))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "creating a view you can never run is refused up front");
    }

    [Fact]
    public async Task Vendor_cannot_run_a_requisition_view()
    {
        var buyer = fx.ClientAs("u_faridah");
        var pr = await Create(buyer, new SaveViewRequest("d3-prs", "Requisition", [], [new("Code", null, null)]));
        (await buyer.PostAsJsonAsync($"/api/views/{pr.Id}/share", new ShareViewRequest(true))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await fx.ClientAs("VU-D3A").GetAsync($"/api/views/{pr.Id}/run"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "PRs are internal (matrix A7)");
    }

    // ---- registry validation ----

    [Fact]
    public async Task Unknown_field_key_is_400_on_save_and_loud_400_on_run()
    {
        var buyer = fx.ClientAs("u_faridah");
        (await buyer.PostAsJsonAsync("/api/views", Rfq("d3-bad", [new("Nope", "Eq", "x", null)])))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var resp = await buyer.GetAsync($"/api/views/{fx.DeadKeyViewId}/run");
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest, "a dead key must fail loudly, never silently drop the filter");
        (await resp.Content.ReadAsStringAsync()).Should().Contain("NoSuchField");
    }

    [Fact]
    public async Task Operator_datatype_mismatch_and_bad_token_are_400()
    {
        var buyer = fx.ClientAs("u_faridah");
        (await buyer.PostAsJsonAsync("/api/views", Rfq("d3-mismatch", [new("ClosesUtc", "Contains", "x", null)])))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "Contains is not valid for an Instant");
        (await buyer.PostAsJsonAsync("/api/views", Rfq("d3-badtoken", [new("ClosesUtc", "Gte", "@nextYear", null)])))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "the token set is gate-driven: @today/@startOfMonth/@endOfMonth only");
    }

    // ---- ownership + sharing ----

    [Fact]
    public async Task Private_views_are_invisible_to_others_and_system_views_are_read_only()
    {
        var buyer = fx.ClientAs("u_faridah");
        var mine = await Create(buyer, Rfq("d3-private", [new("Status", "Eq", "Draft", null)]));

        var lim = fx.ClientAs("u_lim");
        var visible = (await lim.GetFromJsonAsync<List<SavedViewDto>>("/api/views?recordType=Rfq"))!;
        visible.Should().NotContain(v => v.Id == mine.Id, "another user's private view is not listed");
        (await lim.GetAsync($"/api/views/{mine.Id}/run")).StatusCode.Should().Be(HttpStatusCode.NotFound, "existence hidden");

        (await lim.PutAsJsonAsync($"/api/views/{fx.SystemViewId}", Rfq("hack", [])))
            .StatusCode.Should().Be(HttpStatusCode.Conflict, "system views are read-only");
        visible.Should().Contain(v => v.Id == fx.SystemViewId, "system views are visible to everyone");
    }

    [Fact]
    public async Task Only_the_owner_edits_a_shared_view()
    {
        var buyer = fx.ClientAs("u_faridah");
        var view = await Create(buyer, Rfq("d3-owned", [new("Status", "Eq", "Open", null)]));
        (await buyer.PostAsJsonAsync($"/api/views/{view.Id}/share", new ShareViewRequest(true))).StatusCode.Should().Be(HttpStatusCode.OK);

        (await fx.ClientAs("u_lim").PutAsJsonAsync($"/api/views/{view.Id}", Rfq("d3-hijack", [])))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "visible but not owned");
    }

    [Fact]
    public async Task System_view_run_reproduces_the_full_rfq_list_for_internal_users()
    {
        var run = await Run(fx.ClientAs("u_faridah"), fx.SystemViewId);
        run.Columns.Select(c => c.FieldKey).Should().ContainInOrder("Code", "Title", "Envelope", "InvitedCount", "BidCount", "ClosesUtc", "Status");
        Codes(run).Should().BeEquivalentTo(["RFQ-2026-7301", "RFQ-2026-7302", "RFQ-2026-7303"]);
    }
}
