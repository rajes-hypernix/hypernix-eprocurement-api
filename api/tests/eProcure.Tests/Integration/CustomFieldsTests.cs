using System.Net;
using System.Net.Http.Json;
using eProcure.Application.CustomFields;
using eProcure.Application.Dashboards;
using eProcure.Application.Views;
using eProcure.Domain.Procurement;
using eProcure.Domain.Suppliers;
using FluentAssertions;
using Xunit;

namespace eProcure.Tests.Integration;

/// <summary>
/// D5 Phase 2 pins: def CRUD keeps the registry in lockstep (create inserts Kind=Custom,
/// deactivate hides from the palette while a referencing view fails LOUDLY, zero-value
/// hard-delete removes the row, values-ever-written → 409); value writes validate per type
/// and enforce Required; vendors read only reachable records' values and never write; and
/// the D3/D4 chain lights up WITHOUT modification — a Money custom field sums with honest
/// nulls, a Date custom field buckets, both through the untouched endpoints.
/// </summary>
public sealed class CustomFieldsFixture : IAsyncLifetime
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
            var a = new Vendor { Code = "V-D5A", Name = "D5 Alpha", RegisteredName = "D5 Alpha Sdn Bhd" };
            var b = new Vendor { Code = "V-D5B", Name = "D5 Beta", RegisteredName = "D5 Beta Sdn Bhd" };
            db.Vendors.AddRange(a, b);
            db.VendorUsers.AddRange(
                new VendorUser("VU-D5A", a.Id, "Alpha User", "d5a@vendor.test"),
                new VendorUser("VU-D5B", b.Id, "Beta User", "d5b@vendor.test"));

            var poA = new PurchaseOrder { Code = "PO-2026-7501", VendorId = a.Id, CreatedUtc = now, UpdatedUtc = now,
                Lines = { new PoLine { ItemCode = "X1", Description = "x", Qty = 1, UnitPrice = 10, Uom = "Unit" } } };
            var poB = new PurchaseOrder { Code = "PO-2026-7502", VendorId = b.Id, CreatedUtc = now, UpdatedUtc = now,
                Lines = { new PoLine { ItemCode = "X2", Description = "x", Qty = 1, UnitPrice = 10, Uom = "Unit" } } };
            var poC = new PurchaseOrder { Code = "PO-2026-7503", VendorId = a.Id, CreatedUtc = now, UpdatedUtc = now };
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

    public async Task<CustomFieldDefDto> CreateDef(string label, string dataType, string recordType = "PurchaseOrder")
    {
        var resp = await ClientAs("u_admin").PostAsJsonAsync("/api/custom-fields",
            new SaveCustomFieldDefRequest(label, recordType, dataType, null, false, "", 0));
        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        return (await resp.Content.ReadFromJsonAsync<CustomFieldDefDto>())!;
    }

    public async Task SetValue(string persona, Guid recordId, string code, string? value)
    {
        var resp = await ClientAs(persona).PutAsJsonAsync($"/api/custom-values/PurchaseOrder/{recordId}",
            new SaveCustomValuesRequest(new Dictionary<string, string?> { [code] = value }));
        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
    }
}

public sealed class CustomFieldsTests(CustomFieldsFixture fx) : IClassFixture<CustomFieldsFixture>
{
    // ---- defs + registry lockstep ----

    [Fact]
    public async Task Def_create_registers_the_field_and_the_builder_sees_it_grouped_custom()
    {
        var def = await fx.CreateDef("Site Induction Needed", "Bool");
        def.Code.Should().Be("cf_site_induction_needed");

        var fields = await fx.ClientAs("u_faridah").GetFromJsonAsync<List<ViewFieldDto>>("/api/views/fields?recordType=PurchaseOrder");
        var mine = fields!.Single(f => f.FieldKey == def.Code);
        mine.Kind.Should().Be("Custom");
        mine.DataType.Should().Be("Bool");
    }

    [Fact]
    public async Task Deactivation_hides_from_the_palette_and_a_referencing_view_fails_loudly()
    {
        var def = await fx.CreateDef("Deprecated Flag", "Text");
        await fx.SetValue("u_faridah", fx.PoAId, def.Code, "x");

        var buyer = fx.ClientAs("u_faridah");
        var view = await (await buyer.PostAsJsonAsync("/api/views", new SaveViewRequest(
            "d5-dep", "PurchaseOrder",
            [new SavedViewFilterDto(def.Code, "Contains", "x", null)],
            [new SavedViewColumnDto("Code", null, null)]))).Content.ReadFromJsonAsync<SavedViewDto>();

        (await fx.ClientAs("u_admin").PostAsJsonAsync($"/api/custom-fields/{def.Id}/active", false))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var fields = await buyer.GetFromJsonAsync<List<ViewFieldDto>>("/api/views/fields?recordType=PurchaseOrder");
        fields!.Should().NotContain(f => f.FieldKey == def.Code, "deactivated fields hide from the palette");

        var run = await buyer.GetAsync($"/api/views/{view!.Id}/run");
        run.StatusCode.Should().Be(HttpStatusCode.BadRequest, "a view referencing a deactivated key fails LOUDLY (ruled)");
        (await run.Content.ReadAsStringAsync()).Should().Contain(def.Code);
    }

    [Fact]
    public async Task Zero_value_defs_hard_delete_with_their_registry_row_but_valued_defs_never()
    {
        var admin = fx.ClientAs("u_admin");
        var clean = await fx.CreateDef("Typo Field", "Text");
        (await admin.DeleteAsync($"/api/custom-fields/{clean.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await fx.ClientAs("u_faridah").GetFromJsonAsync<List<ViewFieldDto>>("/api/views/fields?recordType=PurchaseOrder"))!
            .Should().NotContain(f => f.FieldKey == clean.Code, "the registry row went with it");

        var used = await fx.CreateDef("Used Field", "Text");
        await fx.SetValue("u_faridah", fx.PoAId, used.Code, "kept");
        (await admin.DeleteAsync($"/api/custom-fields/{used.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.Conflict, "values ever written → deactivate-only, forever (ruled)");
    }

    // ---- value validation + authorization ----

    [Fact]
    public async Task Values_validate_per_type_and_required_is_enforced_at_save()
    {
        var intDef = await fx.CreateDef("Lead Days", "Int");
        var buyer = fx.ClientAs("u_faridah");
        (await buyer.PutAsJsonAsync($"/api/custom-values/PurchaseOrder/{fx.PoAId}",
            new SaveCustomValuesRequest(new() { [intDef.Code] = "2.5" })))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "Int wholeness is a save-time rule (ruled consolidation)");

        var reqResp = await fx.ClientAs("u_admin").PostAsJsonAsync("/api/custom-fields",
            new SaveCustomFieldDefRequest("Mandatory Note", "PurchaseOrder", "Text", null, true, "", 0));
        var reqDef = (await reqResp.Content.ReadFromJsonAsync<CustomFieldDefDto>())!;
        (await buyer.PutAsJsonAsync($"/api/custom-values/PurchaseOrder/{fx.PoAId}",
            new SaveCustomValuesRequest(new() { [reqDef.Code] = "" })))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "required is enforced at value-save (and ONLY there — D7 owns lifecycle gating)");
    }

    [Fact]
    public async Task Vendors_read_only_reachable_records_values_and_never_write()
    {
        var def = await fx.CreateDef("Vendor Visible", "Text");
        await fx.SetValue("u_faridah", fx.PoAId, def.Code, "hello");

        var values = await fx.ClientAs("VU-D5A").GetFromJsonAsync<List<CustomValueDto>>($"/api/custom-values/PurchaseOrder/{fx.PoAId}");
        values!.Single(v => v.Code == def.Code).Value.Should().Be("hello", "vendor A owns the PO");

        (await fx.ClientAs("VU-D5B").GetAsync($"/api/custom-values/PurchaseOrder/{fx.PoAId}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "the scoped record fetch rides along — foreign PO");

        (await fx.ClientAs("VU-D5A").PutAsJsonAsync($"/api/custom-values/PurchaseOrder/{fx.PoAId}",
            new SaveCustomValuesRequest(new() { [def.Code] = "nope" })))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "A67 is Buyer-only (ruled deny-by-default)");
    }

    // ---- the D3/D4 chain lights up unmodified ----

    [Fact]
    public async Task A_money_custom_field_sums_with_honest_nulls_and_a_date_field_buckets()
    {
        var money = await fx.CreateDef("Bank Guarantee", "Money");
        var date = await fx.CreateDef("Warranty Expiry", "Date");
        await fx.SetValue("u_faridah", fx.PoAId, money.Code, "1000.50");
        await fx.SetValue("u_faridah", fx.PoBId, money.Code, "499.50");
        var thisMonth = DateTime.UtcNow.ToString("yyyy-MM");
        await fx.SetValue("u_faridah", fx.PoAId, date.Code, DateTime.UtcNow.ToString("yyyy-MM-15"));

        var buyer = fx.ClientAs("u_faridah");
        var view = await (await buyer.PostAsJsonAsync("/api/views", new SaveViewRequest(
            "d5-agg", "PurchaseOrder", [],
            [new SavedViewColumnDto("Code", null, null), new SavedViewColumnDto(money.Code, null, null)])))
            .Content.ReadFromJsonAsync<SavedViewDto>();

        var sum = await buyer.GetFromJsonAsync<ViewAggregateResult>($"/api/views/{view!.Id}/aggregate?fn=sum&field={money.Code}");
        sum!.Value.Should().Be(1500.00m);
        sum.ExcludedNullCount.Should().Be(1, "the third PO has NO value row — the honest null is counted, never zeroed");

        var series = await buyer.GetFromJsonAsync<ViewSeriesResult>($"/api/views/{view.Id}/series?fn=count&bucket={date.Code}&months=3");
        series!.Buckets.Single(b => b.Bucket == thisMonth).Value.Should().Be(1, "the Date custom field buckets");
        series.UnbucketedCount.Should().Be(2, "two POs have no expiry date — excluded and surfaced");

        // Filter by the custom key, incl. the ruled @today±Nd token FORM (the gate criterion).
        var expiring = await (await buyer.PostAsJsonAsync("/api/views", new SaveViewRequest(
            "d5-expiring", "PurchaseOrder",
            [new SavedViewFilterDto(date.Code, "Lte", "@today+90d", null)],
            [new SavedViewColumnDto("Code", null, null)]))).Content.ReadFromJsonAsync<SavedViewDto>();
        var run = await buyer.GetFromJsonAsync<ViewRunResult>($"/api/views/{expiring!.Id}/run");
        run!.Rows.Should().HaveCount(1);
        run.Rows[0]["Code"]!.ToString().Should().Be("PO-2026-7501");
    }

    // ---- CF4-T12: authoring parity — display type, insert-before, show-in-list ----

    [Fact]
    public async Task Insert_before_places_the_def_in_the_target_slot_and_shifts_the_rest()
    {
        var admin = fx.ClientAs("u_admin");
        var first = await fx.CreateDef("CF4 Order First", "Text", "Invoice");
        var second = await fx.CreateDef("CF4 Order Second", "Text", "Invoice");

        var resp = await admin.PostAsJsonAsync("/api/custom-fields", new SaveCustomFieldDefRequest(
            "CF4 Order Wedge", "Invoice", "Text", null, false, "", 99, InsertBeforeId: second.Id));
        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());

        var defs = (await admin.GetFromJsonAsync<List<CustomFieldDefDto>>("/api/custom-fields?recordType=Invoice"))!
            .Where(d => d.Label.StartsWith("CF4 Order")).OrderBy(d => d.Sort).ThenBy(d => d.Label).Select(d => d.Label).ToList();
        defs.Should().ContainInOrder("CF4 Order First", "CF4 Order Wedge", "CF4 Order Second");
    }

    [Fact]
    public async Task Non_normal_display_fields_reject_user_edits_but_tolerate_unchanged_echoes()
    {
        var admin = fx.ClientAs("u_admin");
        var def = await fx.CreateDef("CF4 Inline Ref", "Text");
        await fx.SetValue("u_faridah", fx.PoCId, def.Code, "stamped-by-system");

        // flip to Inline (display type IS mutable — unlike code/type)
        (await admin.PutAsJsonAsync($"/api/custom-fields/{def.Id}", new SaveCustomFieldDefRequest(
            def.Label, "PurchaseOrder", "Text", null, false, "", def.Sort, DisplayType: "Inline")))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var buyer = fx.ClientAs("u_faridah");
        (await buyer.PutAsJsonAsync($"/api/custom-values/PurchaseOrder/{fx.PoCId}",
            new SaveCustomValuesRequest(new() { [def.Code] = "user-tamper" })))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "an Inline field is display-only — the SERVER refuses the change");
        (await buyer.PutAsJsonAsync($"/api/custom-values/PurchaseOrder/{fx.PoCId}",
            new SaveCustomValuesRequest(new() { [def.Code] = "stamped-by-system" })))
            .StatusCode.Should().Be(HttpStatusCode.OK, "an unchanged echo is tolerated so whole-form saves don't break");

        var read = (await buyer.GetFromJsonAsync<List<CustomValueDto>>($"/api/custom-values/PurchaseOrder/{fx.PoCId}"))!;
        read.Single(v => v.Code == def.Code).Value.Should().Be("stamped-by-system");
        read.Single(v => v.Code == def.Code).DisplayType.Should().Be("Inline");
    }

    [Fact]
    public async Task Show_in_list_appends_the_column_to_SYSTEM_view_runs_only()
    {
        var admin = fx.ClientAs("u_admin");
        var resp = await admin.PostAsJsonAsync("/api/custom-fields", new SaveCustomFieldDefRequest(
            "CF4 List Col", "PurchaseOrder", "Text", null, false, "", 50, ShowInList: true));
        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        var def = (await resp.Content.ReadFromJsonAsync<CustomFieldDefDto>())!;
        await fx.SetValue("u_faridah", fx.PoAId, def.Code, "col-value");

        Guid systemViewId = default;
        await fx.Factory.SeedAsync(db =>
        {
            var view = new Domain.Views.SavedView
            {
                Code = "SYS-CF4-PO", Name = "CF4 System POs", RecordType = Domain.Views.RecordType.PurchaseOrder,
                IsSystem = true, IsShared = true, CreatedUtc = DateTime.UtcNow, UpdatedUtc = DateTime.UtcNow,
            };
            view.Columns.Add(new Domain.Views.SavedViewColumn { FieldKey = "Code", Sort = 0 });
            db.SavedViews.Add(view);
            systemViewId = view.Id;
            return Task.CompletedTask;
        });

        var buyer = fx.ClientAs("u_faridah");
        var run = await buyer.GetFromJsonAsync<ViewRunResult>($"/api/views/{systemViewId}/run");
        run!.Columns.Select(c => c.FieldKey).Should().Contain(def.Code, "show-in-list surfaces on the system view");
        run.Rows.Single(r => r["Code"]!.ToString() == "PO-2026-7501")[def.Code]!.ToString().Should().Be("col-value");

        // A USER-authored view keeps exactly its author's columns — no append.
        var mine = await (await buyer.PostAsJsonAsync("/api/views", new SaveViewRequest(
            "cf4-own", "PurchaseOrder", [], [new SavedViewColumnDto("Code", null, null)])))
            .Content.ReadFromJsonAsync<SavedViewDto>();
        var ownRun = await buyer.GetFromJsonAsync<ViewRunResult>($"/api/views/{mine!.Id}/run");
        ownRun!.Columns.Select(c => c.FieldKey).Should().NotContain(def.Code);
    }

    // TEST-SWEEP-T2 (inventory PART 6): every one of the 8 launch data types creates a def
    // and lands a registry row — pinned as a loop, not sampled.
    [Fact]
    public async Task Every_data_type_creates_a_def_with_its_registry_row()
    {
        var admin = fx.ClientAs("u_admin");
        var listId = ((await (await admin.PostAsJsonAsync("/api/custom-lists",
                new { code = "SWEEPT2", name = "Sweep T2", description = (string?)null, parentListCode = (string?)null }))
            .Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid());

        foreach (var type in new[] { "Text", "LongText", "Int", "Decimal", "Money", "Date", "Bool", "ListValue" })
        {
            var resp = await admin.PostAsJsonAsync("/api/custom-fields", new SaveCustomFieldDefRequest(
                $"Sweep {type}", "Vendor", type, type == "ListValue" ? listId : null, false, "", 0));
            resp.StatusCode.Should().Be(HttpStatusCode.OK, $"{type}: {await resp.Content.ReadAsStringAsync()}");
            var def = (await resp.Content.ReadFromJsonAsync<CustomFieldDefDto>())!;
            def.DataType.Should().Be(type);
            var fields = await admin.GetFromJsonAsync<List<ViewFieldDto>>("/api/views/fields?recordType=Vendor");
            fields!.Should().Contain(x => x.FieldKey == def.Code && x.Kind == "Custom",
                $"the {type} def registers in the D3 palette in the same transaction");
        }
    }

    // ---- CF6-T1: line-scoped custom fields (locked model: nullable LineId discriminator) ----

    [Fact]
    public async Task Line_values_round_trip_per_line_and_never_bleed_into_the_header_grain()
    {
        var admin = fx.ClientAs("u_admin");
        var resp = await admin.PostAsJsonAsync("/api/custom-fields", new SaveCustomFieldDefRequest(
            "CF6 Batch No", "PurchaseOrder", "Text", null, false, "", 0, Scope: "Line"));
        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        var def = (await resp.Content.ReadFromJsonAsync<CustomFieldDefDto>())!;
        def.Scope.Should().Be("Line");

        var lineId = await fx.Factory.LineIdOf(fx.PoAId);
        var buyer = fx.ClientAs("u_faridah");
        (await buyer.PutAsJsonAsync($"/api/custom-values/PurchaseOrder/{fx.PoAId}",
            new SaveCustomValuesRequest(new(), new() { [lineId] = new() { [def.Code] = "LOT-77" } })))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var lines = (await buyer.GetFromJsonAsync<Dictionary<Guid, List<CustomValueDto>>>(
            $"/api/custom-values/PurchaseOrder/{fx.PoAId}/lines"))!;
        lines[lineId].Single(v => v.Code == def.Code).Value.Should().Be("LOT-77");

        // The header read must NOT show the line def (it has no header grain).
        var header = (await buyer.GetFromJsonAsync<List<CustomValueDto>>($"/api/custom-values/PurchaseOrder/{fx.PoAId}"))!;
        header.Should().NotContain(v => v.Code == def.Code);

        // Grain discipline both ways: a line def refuses header writes, and vice versa.
        (await buyer.PutAsJsonAsync($"/api/custom-values/PurchaseOrder/{fx.PoAId}",
            new SaveCustomValuesRequest(new() { [def.Code] = "nope" })))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var headerDef = await fx.CreateDef("CF6 Header Twin", "Text");
        (await buyer.PutAsJsonAsync($"/api/custom-values/PurchaseOrder/{fx.PoAId}",
            new SaveCustomValuesRequest(new(), new() { [lineId] = new() { [headerDef.Code] = "nope" } })))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Line_writes_verify_line_ownership_and_line_defs_stay_out_of_the_view_palette()
    {
        var admin = fx.ClientAs("u_admin");
        var def = (await (await admin.PostAsJsonAsync("/api/custom-fields", new SaveCustomFieldDefRequest(
            "CF6 Ownership", "PurchaseOrder", "Text", null, false, "", 0, Scope: "Line")))
            .Content.ReadFromJsonAsync<CustomFieldDefDto>())!;

        // A line id from ANOTHER record is refused — ownership is server-checked.
        var foreignLine = await fx.Factory.LineIdOf(fx.PoBId);
        (await fx.ClientAs("u_faridah").PutAsJsonAsync($"/api/custom-values/PurchaseOrder/{fx.PoAId}",
            new SaveCustomValuesRequest(new(), new() { [foreignLine] = new() { [def.Code] = "x" } })))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "the line does not belong to the record");

        // Locked deferral: the view runner stays header-grain — no registry row, no palette entry.
        var fields = await fx.ClientAs("u_faridah").GetFromJsonAsync<List<ViewFieldDto>>("/api/views/fields?recordType=PurchaseOrder");
        fields!.Should().NotContain(x => x.FieldKey == def.Code);

        // And show-in-list (a header-list concept) is refused on line defs.
        (await admin.PostAsJsonAsync("/api/custom-fields", new SaveCustomFieldDefRequest(
            "CF6 Bad Flag", "PurchaseOrder", "Text", null, false, "", 0, ShowInList: true, Scope: "Line")))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Scope is immutable after creation.
        (await admin.PutAsJsonAsync($"/api/custom-fields/{def.Id}", new SaveCustomFieldDefRequest(
            def.Label, "PurchaseOrder", "Text", null, false, "", def.Sort, Scope: "Header")))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
