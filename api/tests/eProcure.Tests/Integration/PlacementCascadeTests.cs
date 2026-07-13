using System.Net;
using System.Net.Http.Json;
using eProcure.Application.CustomFields;
using eProcure.Application.Forms;
using FluentAssertions;
using Xunit;

namespace eProcure.Tests.Integration;

/// <summary>
/// CF-FIX4-T4 — the mandatory placement cascade + THE BIDIRECTIONAL INVARIANT (locked):
/// EntryFormField rows are the SOLE placement authority; CustomFieldDefApplication holds
/// applies-to + a default-group HINT only; applies-to ⟺ placements never disagree.
/// Wire-compat seam (logged): Placements NULL = legacy applied-but-unplaced caller;
/// non-null = cascade-aware — every form-bearing applied type must place.
/// </summary>
public sealed class PlacementCascadeTests(EntryFormsFixture fx) : IClassFixture<EntryFormsFixture>
{
    private static string S => Guid.NewGuid().ToString("N")[..6];

    private async Task<(Guid FormId, Guid HeaderId, EntryFormDefDto Dto)> StandardPr()
    {
        var forms = await fx.ClientAs("u_admin").GetFromJsonAsync<List<EntryFormDefDto>>("/api/entry-forms?recordType=Requisition");
        var std = forms!.Single(f => f.IsSystem);
        return (std.Id, std.Groups.Single(g => g.IsHeader).Id, std);
    }

    [Fact]
    public async Task Cascade_aware_create_with_no_placement_for_a_form_bearing_type_is_refused()
    {
        var resp = await fx.ClientAs("u_admin").PostAsJsonAsync("/api/custom-fields", new SaveCustomFieldDefRequest(
            $"T4 NoPlace {S}", "Requisition", "Text", null, false, "", 0,
            Placements: []));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await resp.Content.ReadAsStringAsync()).Should().Contain("needs a form placement");
    }

    [Fact]
    public async Task Legacy_null_Placements_keeps_the_applied_but_unplaced_behavior()
    {
        var resp = await fx.ClientAs("u_admin").PostAsJsonAsync("/api/custom-fields", new SaveCustomFieldDefRequest(
            $"T4 Legacy {S}", "Requisition", "Text", null, false, "", 0));
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "the CF-FIX-2-style wire-compat seam");
    }

    [Fact]
    public async Task The_cascade_writes_THE_placement_row_group_defaults_to_Header_and_the_hint_lands()
    {
        var (formId, headerId, _) = await StandardPr();
        var suffix = S;
        var def = (await (await fx.ClientAs("u_admin").PostAsJsonAsync("/api/custom-fields", new SaveCustomFieldDefRequest(
            $"T4 Cascade {suffix}", "Requisition", "Text", null, false, "", 0,
            Placements: [new FieldPlacementRequest("Requisition", formId, null)])))
            .Content.ReadFromJsonAsync<CustomFieldDefDto>())!;

        await fx.Factory.SeedAsync(db =>
        {
            var row = db.EntryFormFields.Single(f => f.FieldKey == def.Code);
            row.FormDefId.Should().Be(formId, "placement lands on the chosen form — the standard form accepts cascade rows");
            row.GroupId.Should().Be(headerId, "null group resolves to the form's Header (L3 — cannot miss)");
            db.CustomFieldDefApplications.Single(a => a.FieldDefId == def.Id)
                .DefaultGroupTitle.Should().Be("Header", "the HINT is a title, never placement storage");
            return Task.CompletedTask;
        });

        // The SAME row the designer moves: re-group it via the editor path and the field follows.
        var resolved = await fx.ClientAs("u_faridah").GetFromJsonAsync<ResolvedFormDto>("/api/entry-forms/resolve?recordType=Requisition");
        resolved!.Fields.Should().Contain(f => f.FieldKey == def.Code && f.FieldGroup == "Header");
    }

    [Fact]
    public async Task Placement_for_a_type_outside_the_applies_to_set_is_refused()
    {
        var (formId, _, _) = await StandardPr();
        var resp = await fx.ClientAs("u_admin").PostAsJsonAsync("/api/custom-fields", new SaveCustomFieldDefRequest(
            $"T4 Disagree {S}", "Vendor", "Text", null, false, "", 0,
            RecordTypes: ["Vendor"],
            Placements: [new FieldPlacementRequest("Requisition", formId, null)]));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await resp.Content.ReadAsStringAsync()).Should().Contain("can never disagree");
    }

    [Fact]
    public async Task Direction_A_removing_an_applies_to_type_takes_its_placements_with_it()
    {
        var (formId, _, _) = await StandardPr();
        var suffix = S;
        var def = (await (await fx.ClientAs("u_admin").PostAsJsonAsync("/api/custom-fields", new SaveCustomFieldDefRequest(
            $"T4 DirA {suffix}", "Requisition", "Text", null, false, "", 0,
            RecordTypes: ["Requisition", "Vendor"],
            Placements: [new FieldPlacementRequest("Requisition", formId, null)])))
            .Content.ReadFromJsonAsync<CustomFieldDefDto>())!;

        // Drop Requisition from applies-to (zero values — allowed) → the placement row goes too.
        (await fx.ClientAs("u_admin").PutAsJsonAsync($"/api/custom-fields/{def.Id}", new SaveCustomFieldDefRequest(
            def.Label, "Vendor", "Text", null, false, "", 0, RecordTypes: ["Vendor"])))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        await fx.Factory.SeedAsync(db =>
        {
            db.EntryFormFields.Any(f => f.FieldKey == def.Code).Should().BeFalse("applies-to removal unplaces (direction A)");
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Direction_B_losing_the_last_placement_drops_the_application_unless_values_pin_it()
    {
        var (formId, _, std) = await StandardPr();
        var suffix = S;
        var admin = fx.ClientAs("u_admin");
        // A non-system form to remove from (system forms refuse edits).
        var form = (await (await admin.PostAsJsonAsync("/api/entry-forms",
            new SaveEntryFormRequest($"T4 DirB {suffix}", "Requisition", [EntryFormsFixture.Field("Department", 0)])))
            .Content.ReadFromJsonAsync<EntryFormDefDto>())!;
        var def = (await (await admin.PostAsJsonAsync("/api/custom-fields", new SaveCustomFieldDefRequest(
            $"T4 DirB Field {suffix}", "Requisition", "Text", null, false, "", 0,
            Placements: [new FieldPlacementRequest("Requisition", form.Id, null)])))
            .Content.ReadFromJsonAsync<CustomFieldDefDto>())!;

        // Remove the field from the ONLY form placing it → application + registry drop.
        (await admin.PutAsJsonAsync($"/api/entry-forms/{form.Id}",
            new SaveEntryFormRequest(form.Name, "Requisition", [EntryFormsFixture.Field("Department", 0)])))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        await fx.Factory.SeedAsync(db =>
        {
            db.CustomFieldDefApplications.Any(a => a.FieldDefId == def.Id).Should().BeFalse(
                "last placement removal drops the applies-to (direction B, locked)");
            db.FieldRegistry.Any(r => r.CustomFieldDefId == def.Id).Should().BeFalse();
            db.CustomFieldDefs.Any(d => d.Id == def.Id).Should().BeTrue("the DEF itself survives — only the application drops");
            return Task.CompletedTask;
        });

        // Values PIN the application: place again, store a value, remove again → application STAYS.
        (await admin.PutAsJsonAsync($"/api/custom-fields/{def.Id}", new SaveCustomFieldDefRequest(
            def.Label, "Requisition", "Text", null, false, "", 0, RecordTypes: ["Requisition"],
            Placements: [new FieldPlacementRequest("Requisition", form.Id, null)])))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var buyer = fx.ClientAs("u_faridah");
        var pr = (await (await buyer.PostAsJsonAsync("/api/requisitions", EntryFormsFixture.Pr())).Content
            .ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
        (await buyer.PutAsJsonAsync($"/api/custom-values/Requisition/{pr}",
            new { values = new Dictionary<string, string?> { [def.Code] = "pinning value" } })).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"/api/entry-forms/{form.Id}",
            new SaveEntryFormRequest(form.Name, "Requisition", [EntryFormsFixture.Field("Department", 0)])))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        await fx.Factory.SeedAsync(db =>
        {
            db.CustomFieldDefApplications.Any(a => a.FieldDefId == def.Id).Should().BeTrue(
                "stored values pin the application — data safety outranks tidiness (L6 is the higher law)");
            return Task.CompletedTask;
        });
        var values = await buyer.GetFromJsonAsync<List<CustomValueDto>>($"/api/custom-values/Requisition/{pr}");
        values!.Single(v => v.Code == def.Code).Value.Should().Be("pinning value", "the value stayed visible throughout");
    }

    [Fact]
    public async Task A_placement_naming_a_wrong_form_or_foreign_group_is_refused()
    {
        var (formId, _, _) = await StandardPr();
        var admin = fx.ClientAs("u_admin");
        (await admin.PostAsJsonAsync("/api/custom-fields", new SaveCustomFieldDefRequest(
            $"T4 BadForm {S}", "Requisition", "Text", null, false, "", 0,
            Placements: [new FieldPlacementRequest("Requisition", Guid.NewGuid(), null)])))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsJsonAsync("/api/custom-fields", new SaveCustomFieldDefRequest(
            $"T4 BadGroup {S}", "Requisition", "Text", null, false, "", 0,
            Placements: [new FieldPlacementRequest("Requisition", formId, Guid.NewGuid())])))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
