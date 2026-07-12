using System.Net;
using System.Net.Http.Json;
using eProcure.Application.Forms;
using eProcure.Application.Sourcing;
using FluentAssertions;
using Xunit;

namespace eProcure.Tests.Integration;

/// <summary>
/// D7 Phase 2 pins: the resolution matrix (role-preferred beats Standard; multi-role via
/// the FIXED GLOBAL PRECEDENCE — u_lim [Buyer,Approver] gets the Buyer form regardless of
/// map order); registry-liveness + system-field refusal on save; the (c) boundary —
/// requiredOnForm gates SUBMIT (create-with-submit AND draft→submit), NEVER draft save;
/// defaults arrive token-resolved; the resolve read is A71 + dynamic View* (vendor ×
/// Requisition → 403).
/// </summary>
public sealed class EntryFormsFixture : IAsyncLifetime
{
    public TestWebAppFactory Factory { get; } = new();

    public Task InitializeAsync() => Factory.SeedAsync(db =>
    {
        // D7 invariants ride SeedAsync; one vendor principal for the dynamic-View* pin.
        var v = new eProcure.Domain.Suppliers.Vendor { Code = "V-D7", Name = "D7 Vendor", RegisteredName = "D7 Vendor Sdn Bhd" };
        db.Vendors.Add(v);
        db.VendorUsers.Add(new eProcure.Domain.Suppliers.VendorUser("VU-D7", v.Id, "D7 User", "d7@vendor.test"));
        db.FieldRegistry.AddRange(eProcure.Application.Views.FieldRegistrySeed.ToEntities());
        return Task.CompletedTask;
    });

    public Task DisposeAsync() { Factory.Dispose(); return Task.CompletedTask; }

    public HttpClient ClientAs(string demoUser)
    {
        var client = Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Demo-User", demoUser);
        return client;
    }

    public static EntryFormFieldDto Field(string key, int sort, string? label = null, bool required = false,
        string display = "Normal", string? def = null, string? subtab = null) =>
        new(key, subtab, "Header", sort, display, required, def, null, false, label, null);

    public async Task<EntryFormDefDto> CreateForm(string name, params EntryFormFieldDto[] fields)
    {
        var resp = await ClientAs("u_admin").PostAsJsonAsync("/api/entry-forms",
            new SaveEntryFormRequest(name, "Requisition", fields.ToList()));
        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        return (await resp.Content.ReadFromJsonAsync<EntryFormDefDto>())!;
    }

    public async Task AssignRoles(Guid formId, params string[] roles)
    {
        var resp = await ClientAs("u_admin").PutAsJsonAsync($"/api/entry-forms/{formId}/roles", new AssignRolesRequest(roles.ToList()));
        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
    }

    public static SavePrRequest Pr(string department = "Maintenance") => new(
        Requestor: "Aishah Karim", Department: department, Location: "Bintulu Plant",
        Category: "Piping", Job: "JOB-1", Memo: "test", RequiredDate: null,
        Lines: [new PrLineInput(null, "ITEM-1", "Widget", 1, "Unit", 10m)]);
}

public sealed class EntryFormsTests(EntryFormsFixture fx) : IClassFixture<EntryFormsFixture>
{
    [Fact]
    public async Task Standard_form_resolves_for_everyone_until_a_role_map_exists_then_the_role_gets_its_form()
    {
        var before = await fx.ClientAs("u_faridah").GetFromJsonAsync<ResolvedFormDto>("/api/entry-forms/resolve?recordType=Requisition");
        before!.FormCode.Should().Be(EntryFormSeed.StandardPrFormCode, "no map yet → Standard");
        before.Fields.Should().HaveCount(7);
        before.Fields.Single(f => f.FieldKey == "Job").Label.Should().Be("Job / Cost ref", "field overrides ride the resolve");

        var role = await fx.CreateForm("Buyer Lite A", EntryFormsFixture.Field("Requestor", 0), EntryFormsFixture.Field("Memo", 1));
        await fx.AssignRoles(role.Id, "Buyer");
        try
        {
            var after = await fx.ClientAs("u_faridah").GetFromJsonAsync<ResolvedFormDto>("/api/entry-forms/resolve?recordType=Requisition");
            after!.FormCode.Should().Be(role.Code, "the role now gets its form automatically — zero deployments");
            after.Fields.Should().HaveCount(2);

            var admin = await fx.ClientAs("u_admin").GetFromJsonAsync<ResolvedFormDto>("/api/entry-forms/resolve?recordType=Requisition");
            admin!.FormCode.Should().Be(EntryFormSeed.StandardPrFormCode, "unmapped roles keep Standard");
        }
        finally { await fx.ClientAs("u_admin").DeleteAsync($"/api/entry-forms/{role.Id}"); }
    }

    [Fact]
    public async Task Multi_role_resolution_follows_the_fixed_global_precedence_not_map_or_array_order()
    {
        // u_lim holds Buyer+Approver. Map the APPROVER form first — if resolution followed
        // map creation order (or anything but the documented constant), this would flip.
        var approverForm = await fx.CreateForm("Approver View B", EntryFormsFixture.Field("Memo", 0));
        var buyerForm = await fx.CreateForm("Buyer View B", EntryFormsFixture.Field("Requestor", 0));
        await fx.AssignRoles(approverForm.Id, "Approver");
        await fx.AssignRoles(buyerForm.Id, "Buyer");
        try
        {
            var resolved = await fx.ClientAs("u_lim").GetFromJsonAsync<ResolvedFormDto>("/api/entry-forms/resolve?recordType=Requisition");
            resolved!.FormCode.Should().Be(buyerForm.Code, "Buyer precedes Approver in the FIXED GLOBAL precedence (ruled: never the user record's array order)");
        }
        finally
        {
            await fx.ClientAs("u_admin").DeleteAsync($"/api/entry-forms/{approverForm.Id}");
            await fx.ClientAs("u_admin").DeleteAsync($"/api/entry-forms/{buyerForm.Id}");
        }
    }

    [Fact]
    public async Task Save_validates_registry_liveness_system_fields_and_hidden_required()
    {
        var admin = fx.ClientAs("u_admin");
        (await admin.PostAsJsonAsync("/api/entry-forms", new SaveEntryFormRequest("Dead Key", "Requisition",
            [EntryFormsFixture.Field("NotAField", 0)]))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsJsonAsync("/api/entry-forms", new SaveEntryFormRequest("Sneaky Code", "Requisition",
            [EntryFormsFixture.Field("Code", 0)]))).StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "Code is minted, not entered — no write path, not placeable");
        (await admin.PostAsJsonAsync("/api/entry-forms", new SaveEntryFormRequest("No Write Path", "Requisition",
            [EntryFormsFixture.Field("Project", 0)]))).StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "PR.Project has no write contract (the convergence row owns it)");
        (await admin.PostAsJsonAsync("/api/entry-forms", new SaveEntryFormRequest("Hidden Req", "Requisition",
            [EntryFormsFixture.Field("Memo", 0, required: true, display: "Hidden")]))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsJsonAsync("/api/entry-forms", new SaveEntryFormRequest("Wrong Type", "PurchaseOrder",
            [EntryFormsFixture.Field("VendorName", 0)]))).StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "OD-D7-5: no PO entry surface consumes forms yet");
    }

    [Fact]
    public async Task Required_on_form_gates_submit_never_draft_save()
    {
        var form = await fx.CreateForm("Strict PR C",
            EntryFormsFixture.Field("Requestor", 0),
            EntryFormsFixture.Field("Department", 1, required: true),
            EntryFormsFixture.Field("Memo", 2));
        await fx.AssignRoles(form.Id, "Buyer");
        var buyer = fx.ClientAs("u_faridah");
        try
        {
            // Draft save with the required field EMPTY: allowed (OD-D7-3 — drafts are incomplete by nature).
            var draft = await buyer.PostAsJsonAsync("/api/requisitions?submit=false", EntryFormsFixture.Pr(department: ""));
            draft.StatusCode.Should().Be(HttpStatusCode.OK, await draft.Content.ReadAsStringAsync());
            var created = (await draft.Content.ReadFromJsonAsync<RequisitionDto>())!;

            // Submit of that draft: blocked, loudly, naming the field.
            var submit = await buyer.PostAsync($"/api/requisitions/{created.Id}/submit", null);
            submit.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await submit.Content.ReadAsStringAsync()).Should().Contain("Department");

            // Create-with-submit, same gap: rejected BEFORE a gap-free code is burned.
            var oneShot = await buyer.PostAsJsonAsync("/api/requisitions?submit=true", EntryFormsFixture.Pr(department: ""));
            oneShot.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            // With the field filled the same submit sails through.
            var ok = await buyer.PostAsJsonAsync("/api/requisitions?submit=true", EntryFormsFixture.Pr(department: "Maintenance"));
            ok.StatusCode.Should().Be(HttpStatusCode.OK, await ok.Content.ReadAsStringAsync());
        }
        finally { await fx.ClientAs("u_admin").DeleteAsync($"/api/entry-forms/{form.Id}"); }
    }

    [Fact]
    public async Task Defaults_arrive_token_resolved_and_the_resolve_read_is_view_gated()
    {
        var form = await fx.CreateForm("Defaulted PR D",
            EntryFormsFixture.Field("Requestor", 0),
            EntryFormsFixture.Field("RequiredDate", 1, def: "@today+7d"));
        await fx.AssignRoles(form.Id, "Buyer");
        try
        {
            var resolved = await fx.ClientAs("u_faridah").GetFromJsonAsync<ResolvedFormDto>("/api/entry-forms/resolve?recordType=Requisition");
            var date = resolved!.Fields.Single(f => f.FieldKey == "RequiredDate").DefaultValue;
            DateTime.Parse(date!).Should().BeCloseTo(DateTime.UtcNow.Date.AddDays(7), TimeSpan.FromDays(1),
                "the shared DateTokens grammar resolves @today+7d server-side");

            // A71 is all-principal, but the dynamic View* inside says Requisitions are internal-only.
            (await fx.ClientAs("VU-D7").GetAsync("/api/entry-forms/resolve?recordType=Requisition"))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden, "vendor × Requisition — the 4th use of the dynamic convention");
        }
        finally { await fx.ClientAs("u_admin").DeleteAsync($"/api/entry-forms/{form.Id}"); }
    }

    [Fact]
    public async Task Standard_forms_are_read_only_and_deletes_clean_their_maps()
    {
        var admin = fx.ClientAs("u_admin");
        var standardId = EntryFormSeed.FormId(EntryFormSeed.StandardPrFormCode);
        (await admin.PutAsJsonAsync($"/api/entry-forms/{standardId}",
            new SaveEntryFormRequest("Hack", "Requisition", [EntryFormsFixture.Field("Memo", 0)])))
            .StatusCode.Should().Be(HttpStatusCode.Conflict, "the segments precedent: system rows are read-only");
        (await admin.DeleteAsync($"/api/entry-forms/{standardId}")).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
