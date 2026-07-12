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

            var poA = new PurchaseOrder { Code = "PO-2026-7501", VendorId = a.Id, CreatedUtc = now, UpdatedUtc = now };
            var poB = new PurchaseOrder { Code = "PO-2026-7502", VendorId = b.Id, CreatedUtc = now, UpdatedUtc = now };
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
}
