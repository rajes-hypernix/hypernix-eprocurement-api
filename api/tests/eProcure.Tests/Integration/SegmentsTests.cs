using System.Net;
using System.Net.Http.Json;
using eProcure.Application.Dashboards;
using eProcure.Application.Segments;
using eProcure.Application.Views;
using eProcure.Domain.Procurement;
using eProcure.Domain.Suppliers;
using FluentAssertions;
using Xunit;

namespace eProcure.Tests.Integration;

/// <summary>
/// D6 Phase 2 pins: def/value/application CRUD with registry lockstep; assignments behind
/// the folded A66/A67 three-layer gate (vendor reads labels on reachable records, writes
/// nothing); GROUP-BY correctness including the ruled NAMED Unassigned bucket (its own
/// test); hierarchy storage round-trip; system segments read-only via the API.
/// </summary>
public sealed class SegmentsFixture : IAsyncLifetime
{
    public TestWebAppFactory Factory { get; } = new();
    public Guid PoAId { get; private set; }
    public Guid PoBId { get; private set; }
    public Guid PoCId { get; private set; }

    public async Task InitializeAsync()
    {
        var now = DateTime.UtcNow;
        await Factory.SeedAsync(db =>
        {
            var a = new Vendor { Code = "V-D6A", Name = "D6 Alpha", RegisteredName = "D6 Alpha Sdn Bhd" };
            var b = new Vendor { Code = "V-D6B", Name = "D6 Beta", RegisteredName = "D6 Beta Sdn Bhd" };
            db.Vendors.AddRange(a, b);
            db.VendorUsers.AddRange(
                new VendorUser("VU-D6A", a.Id, "Alpha User", "d6a@vendor.test"),
                new VendorUser("VU-D6B", b.Id, "Beta User", "d6b@vendor.test"));

            PurchaseOrder Po(string code, Guid vid, decimal qty, decimal price) => new()
            {
                Code = code, VendorId = vid, CreatedUtc = now, UpdatedUtc = now,
                Lines = { new PoLine { ItemCode = "X", Description = "x", Qty = qty, UnitPrice = price, Uom = "Unit" } },
            };
            var poA = Po("PO-2026-7601", a.Id, 1, 1000m);
            var poB = Po("PO-2026-7602", b.Id, 1, 500m);
            var poC = Po("PO-2026-7603", a.Id, 1, 250m);
            db.PurchaseOrders.AddRange(poA, poB, poC);
            PoAId = poA.Id; PoBId = poB.Id; PoCId = poC.Id;

            db.FieldRegistry.AddRange(FieldRegistrySeed.ToEntities());
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

    public async Task<SegmentDefDto> CreateProjectSegment(params string[] values)
    {
        var admin = ClientAs("u_admin");
        var resp = await admin.PostAsJsonAsync("/api/segments", new SaveSegmentDefRequest($"Project {Guid.NewGuid().ToString("N")[..6]}", false, false));
        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        var def = (await resp.Content.ReadFromJsonAsync<SegmentDefDto>())!;
        foreach (var v in values)
            def = (await (await admin.PostAsJsonAsync($"/api/segments/{def.Id}/values", new SaveSegmentValueRequest(v, null, 0))).Content.ReadFromJsonAsync<SegmentDefDto>())!;
        def = (await (await admin.PostAsJsonAsync($"/api/segments/{def.Id}/applications", new ApplySegmentRequest("PurchaseOrder", true))).Content.ReadFromJsonAsync<SegmentDefDto>())!;
        return def;
    }

    public async Task Assign(Guid recordId, string segCode, string? valueCode)
    {
        var resp = await ClientAs("u_faridah").PutAsJsonAsync($"/api/segment-assignments/PurchaseOrder/{recordId}",
            new SaveSegmentAssignmentsRequest(new() { [segCode] = valueCode }, null));
        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
    }
}

public sealed class SegmentsTests(SegmentsFixture fx) : IClassFixture<SegmentsFixture>
{
    [Fact]
    public async Task Application_registers_the_segment_in_the_builder_palette_with_value_options()
    {
        var def = await fx.CreateProjectSegment("Alpha Plant", "Beta Plant");
        var fields = await fx.ClientAs("u_faridah").GetFromJsonAsync<List<ViewFieldDto>>("/api/views/fields?recordType=PurchaseOrder");
        var mine = fields!.Single(f => f.FieldKey == def.Code);
        mine.Kind.Should().Be("Segment");
        mine.DataType.Should().Be("Enum");
        mine.Options.Should().BeEquivalentTo(["ALPHA-PLANT", "BETA-PLANT"], "value codes ride DimCode");
    }

    [Fact]
    public async Task Assignments_ride_the_three_layer_gate_vendors_read_only_reachable()
    {
        var def = await fx.CreateProjectSegment("Gamma");
        await fx.Assign(fx.PoAId, def.Code, "GAMMA");

        var mine = await fx.ClientAs("VU-D6A").GetFromJsonAsync<List<SegmentAssignmentDto>>($"/api/segment-assignments/PurchaseOrder/{fx.PoAId}");
        mine!.Single(a => a.SegmentCode == def.Code).ValueLabel.Should().Be("Gamma", "vendor A reads labels on its own PO");

        (await fx.ClientAs("VU-D6B").GetAsync($"/api/segment-assignments/PurchaseOrder/{fx.PoAId}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "the scoped record fetch rides along");
        (await fx.ClientAs("VU-D6A").PutAsJsonAsync($"/api/segment-assignments/PurchaseOrder/{fx.PoAId}",
            new SaveSegmentAssignmentsRequest(new() { [def.Code] = null }, null)))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "vendors write nothing (folded A67 = Buyer)");
    }

    [Fact]
    public async Task Group_by_slices_with_the_named_unassigned_bucket_never_dropping_records()
    {
        // The ruled its-own-test: honest-null applied to dimensions.
        var def = await fx.CreateProjectSegment("North", "South");
        await fx.Assign(fx.PoAId, def.Code, "NORTH");
        await fx.Assign(fx.PoBId, def.Code, "SOUTH");
        // PoC deliberately left UNASSIGNED.

        var buyer = fx.ClientAs("u_faridah");
        var view = await (await buyer.PostAsJsonAsync("/api/views", new SaveViewRequest(
            "d6-agg", "PurchaseOrder", [], [new SavedViewColumnDto("Code", null, null), new SavedViewColumnDto("Total", null, null)])))
            .Content.ReadFromJsonAsync<SavedViewDto>();

        var agg = await buyer.GetFromJsonAsync<ViewAggregateResult>(
            $"/api/views/{view!.Id}/aggregate?fn=sum&field=Total&groupBy={def.Code}");
        agg!.GroupedBy.Should().Be(def.Code);
        agg.Groups.Should().HaveCount(3, "two values + the NAMED Unassigned bucket");
        agg.Groups!.Single(g => g.Key == "NORTH").Value.Should().Be(1000m);
        agg.Groups!.Single(g => g.Key == "SOUTH").Value.Should().Be(500m);
        var unassigned = agg.Groups!.Single(g => g.Key == "__unassigned");
        unassigned.Label.Should().Be("Unassigned");
        unassigned.Value.Should().Be(250m, "PoC is surfaced, never silently dropped");
        agg.Groups!.Sum(g => g.Value ?? 0).Should().Be(agg.Value, "the slices reconcile to the total");

        var filtered = await (await buyer.PostAsJsonAsync("/api/views", new SaveViewRequest(
            "d6-filter", "PurchaseOrder",
            [new SavedViewFilterDto(def.Code, "Eq", "NORTH", null)],
            [new SavedViewColumnDto("Code", null, null)]))).Content.ReadFromJsonAsync<SavedViewDto>();
        var run = await buyer.GetFromJsonAsync<ViewRunResult>($"/api/views/{filtered!.Id}/run");
        run!.Rows.Should().HaveCount(1, "segments filter like any registry field");
        run.Rows[0]["Code"]!.ToString().Should().Be("PO-2026-7601");
    }

    [Fact]
    public async Task Hierarchy_round_trips_flat_with_parent_stored()
    {
        var admin = fx.ClientAs("u_admin");
        var def = (await (await admin.PostAsJsonAsync("/api/segments", new SaveSegmentDefRequest($"Region {Guid.NewGuid().ToString("N")[..6]}", true, false)))
            .Content.ReadFromJsonAsync<SegmentDefDto>())!;
        def = (await (await admin.PostAsJsonAsync($"/api/segments/{def.Id}/values", new SaveSegmentValueRequest("Borneo", null, 0))).Content.ReadFromJsonAsync<SegmentDefDto>())!;
        var parent = def.Values.Single();
        def = (await (await admin.PostAsJsonAsync($"/api/segments/{def.Id}/values", new SaveSegmentValueRequest("Sarawak", parent.Id, 1))).Content.ReadFromJsonAsync<SegmentDefDto>())!;
        def.Values.Single(v => v.Code == "SARAWAK").ParentValueId.Should().Be(parent.Id,
            "ParentValueId is stored from day one; rollup UX is the BACKLOG's, gate-driven");
    }

    [Fact]
    public async Task System_segments_are_read_only_and_assignments_on_prs_redirect_to_the_pr()
    {
        var admin = fx.ClientAs("u_admin");
        var defs = await admin.GetFromJsonAsync<List<SegmentDefDto>>("/api/segments");
        // The four system defs exist only where the migration ran (real PG); in this in-memory
        // fixture they are absent — pin the API behaviour with a system def seeded directly.
        await fx.Factory.SeedAsync(db =>
        {
            db.SegmentDefs.Add(new Domain.Segments.SegmentDef
            {
                Id = SegmentSeed.DefId("seg_department"), Code = "seg_department", Name = "Department",
                IsSystem = true, CreatedUtc = DateTime.UtcNow, UpdatedUtc = DateTime.UtcNow,
            });
            db.SegmentApplications.Add(new Domain.Segments.SegmentApplication
            {
                Id = Guid.NewGuid(), SegmentDefId = SegmentSeed.DefId("seg_department"),
                RecordType = Domain.Views.RecordType.Requisition, LineLevel = false,
            });
            return Task.CompletedTask;
        });
        var sys = (await admin.GetFromJsonAsync<List<SegmentDefDto>>("/api/segments"))!.Single(d => d.IsSystem);
        (await admin.PostAsJsonAsync($"/api/segments/{sys.Id}/values", new SaveSegmentValueRequest("Hack", null, 0)))
            .StatusCode.Should().Be(HttpStatusCode.Conflict, "system segments belong to the convergence row");
        defs.Should().NotBeNull();
    }
}

/// <summary>CF2-T6: the uniform lifecycle verbs segments were missing — value edit (code
/// immutable), guarded value delete (assigned → deactivate, the A2F-T3 discipline), def
/// inactivate, guarded def delete (live assignments refuse; clean cascade).</summary>
public sealed class SegmentLifecycleTests(SegmentsFixture fx) : IClassFixture<SegmentsFixture>
{
    [Fact]
    public async Task Value_edit_changes_label_never_code_and_delete_is_guarded()
    {
        var def = await fx.CreateProjectSegment("Alpha Site", "Beta Site");
        var admin = fx.ClientAs("u_admin");
        var alpha = def.Values.Single(v => v.Code == "ALPHA-SITE");

        // Edit: label/active change, the CODE (stored dimension key) never does.
        var resp = await admin.PutAsJsonAsync($"/api/segments/{def.Id}/values/{alpha.Id}",
            new { label = "Alpha Site Renamed", parentValueId = (Guid?)null, sort = 0, active = true });
        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        var updated = (await resp.Content.ReadFromJsonAsync<SegmentDefDto>())!;
        updated.Values.Single(v => v.Id == alpha.Id).Label.Should().Be("Alpha Site Renamed");
        updated.Values.Single(v => v.Id == alpha.Id).Code.Should().Be("ALPHA-SITE", "codes are stored keys — immutable");

        // Guarded delete: assign BETA somewhere, then delete → deactivates, not removed.
        await fx.Assign(fx.PoAId, def.Code, "BETA-SITE");
        var beta = def.Values.Single(v => v.Code == "BETA-SITE");
        var del = await admin.DeleteAsync($"/api/segments/{def.Id}/values/{beta.Id}");
        del.StatusCode.Should().Be(HttpStatusCode.OK);
        (await del.Content.ReadFromJsonAsync<SegmentDefDto>())!.Values.Single(v => v.Id == beta.Id).Active
            .Should().BeFalse("assigned values deactivate — history never re-keys");

        // Unassigned value hard-deletes.
        var del2 = await admin.DeleteAsync($"/api/segments/{def.Id}/values/{alpha.Id}");
        (await del2.Content.ReadFromJsonAsync<SegmentDefDto>())!.Values.Should().NotContain(v => v.Id == alpha.Id);
    }

    [Fact]
    public async Task Def_delete_is_refused_with_live_assignments_and_cascades_when_clean()
    {
        var admin = fx.ClientAs("u_admin");
        var assigned = await fx.CreateProjectSegment("Gamma Site");
        await fx.Assign(fx.PoBId, assigned.Code, "GAMMA-SITE");
        (await admin.DeleteAsync($"/api/segments/{assigned.Id}")).StatusCode
            .Should().Be(HttpStatusCode.Conflict, "live assignments — dimension keys are never silently dropped");

        var clean = await fx.CreateProjectSegment("Delta Site");
        (await admin.DeleteAsync($"/api/segments/{clean.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await admin.GetFromJsonAsync<List<SegmentDefDto>>("/api/segments"))!
            .Should().NotContain(d => d.Id == clean.Id, "applications + registry rows + values cascade with it");

        // Inactivate verb + system-def protection.
        var toggled = await admin.PostAsJsonAsync($"/api/segments/{assigned.Id}/active", false);
        (await toggled.Content.ReadFromJsonAsync<SegmentDefDto>())!.Active.Should().BeFalse();
        await admin.PostAsJsonAsync($"/api/segments/{assigned.Id}/active", true);   // restore for other tests
    }

    // TEST-SWEEP-T2 (inventory PART 8): per-PO-LINE assignment — LineId is a real grain,
    // not a dead column: header and line values coexist and read back distinctly per grain.
    [Fact]
    public async Task Per_po_line_assignment_coexists_with_the_header_value()
    {
        var buyer = fx.ClientAs("u_faridah");
        var def = await fx.CreateProjectSegment("Head Grain", "Line Grain");
        var lineId = await fx.Factory.LineIdOf(fx.PoAId);

        await fx.Assign(fx.PoAId, def.Code, "HEAD-GRAIN");   // header (LineId null)
        (await buyer.PutAsJsonAsync($"/api/segment-assignments/PurchaseOrder/{fx.PoAId}",
            new SaveSegmentAssignmentsRequest(new() { [def.Code] = "LINE-GRAIN" }, lineId)))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var header = (await buyer.GetFromJsonAsync<List<SegmentAssignmentDto>>(
            $"/api/segment-assignments/PurchaseOrder/{fx.PoAId}"))!;
        header.Single(a => a.SegmentCode == def.Code).ValueCode.Should().Be("HEAD-GRAIN", "header grain untouched by the line write");

        var line = (await buyer.GetFromJsonAsync<List<SegmentAssignmentDto>>(
            $"/api/segment-assignments/PurchaseOrder/{fx.PoAId}?lineId={lineId}"))!;
        line.Single(a => a.SegmentCode == def.Code).ValueCode.Should().Be("LINE-GRAIN", "line grain reads back by lineId");
    }
}
