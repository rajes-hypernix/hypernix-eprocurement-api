using System.Net;
using System.Net.Http.Json;
using eProcure.Application.CustomFields;
using eProcure.Application.Forms;
using FluentAssertions;
using Xunit;

namespace eProcure.Tests.Integration;

/// <summary>
/// CF-FIX4-T8 — the ARCHIVE tier: reversible, values hidden from EVERY live surface by
/// ONE central predicate (CustomFieldVisibility), preserved verbatim in storage, both
/// directions audited. Distinct from remove-from-form (L6: other surfaces keep showing
/// the value) and from Purge (irreversible, A73). Views referencing an archived field
/// keep RUNNING (run-with-blanks): archive must never break a shared view.
/// </summary>
public sealed class ArchiveTierTests(EntryFormsFixture fx) : IClassFixture<EntryFormsFixture>
{
    private static string S => Guid.NewGuid().ToString("N")[..6];

    [Fact]
    public async Task Archive_hides_the_value_from_record_read_view_run_palette_and_form_then_unarchive_restores_everything()
    {
        var suffix = S;
        var admin = fx.ClientAs("u_admin");
        var buyer = fx.ClientAs("u_faridah");

        // A placed field with a real value on a real PR, referenced by a view. CFF-T2: custom
        // fields place on a CUSTOM form (the standard form refuses custom placements), so the
        // form-surface checks resolve THAT form by id.
        var form = (await (await admin.PostAsJsonAsync("/api/entry-forms",
            new SaveEntryFormRequest($"T8 Form {suffix}", "Requisition", [EntryFormsFixture.Field("Department", 0)])))
            .Content.ReadFromJsonAsync<EntryFormDefDto>())!;
        var def = (await (await admin.PostAsJsonAsync("/api/custom-fields", new SaveCustomFieldDefRequest(
            $"T8 Probe {suffix}", "Requisition", "Text", null, false, "", 0,
            Placements: [new FieldPlacementRequest("Requisition", form.Id, null)])))
            .Content.ReadFromJsonAsync<CustomFieldDefDto>())!;
        var pr = (await (await buyer.PostAsJsonAsync("/api/requisitions", EntryFormsFixture.Pr())).Content
            .ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
        (await buyer.PutAsJsonAsync($"/api/custom-values/Requisition/{pr}",
            new { values = new Dictionary<string, string?> { [def.Code] = "hidden treasure" } })).EnsureSuccessStatusCode();
        var view = await (await buyer.PostAsJsonAsync("/api/views", new
        {
            name = $"t8-view-{suffix}",
            recordType = "Requisition",
            filters = Array.Empty<object>(),
            columns = new[] { new { fieldKey = "Code", label = (string?)null, sortDirection = (string?)null },
                              new { fieldKey = def.Code, label = (string?)null, sortDirection = (string?)null } },
        })).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var viewId = view.GetProperty("id").GetGuid();

        // BEFORE: visible everywhere.
        (await buyer.GetFromJsonAsync<List<CustomValueDto>>($"/api/custom-values/Requisition/{pr}"))!
            .Should().Contain(v => v.Code == def.Code && v.Value == "hidden treasure");

        // ARCHIVE.
        var archived = (await (await admin.PostAsync($"/api/custom-fields/{def.Id}/archive", null))
            .Content.ReadFromJsonAsync<CustomFieldDefDto>())!;
        archived.Archived.Should().BeTrue();

        // 1. Record read: gone.
        (await buyer.GetFromJsonAsync<List<CustomValueDto>>($"/api/custom-values/Requisition/{pr}"))!
            .Should().NotContain(v => v.Code == def.Code);
        // 2. View run: the view still RUNS; the column renders blank (run-with-blanks).
        var run = await (await buyer.GetAsync($"/api/views/{viewId}/run?page=1&size=200")).Content
            .ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var rows = run.GetProperty("rows").EnumerateArray().ToList();
        rows.Should().NotBeEmpty("archive must never break a shared view");
        rows.Any(r => r.TryGetProperty(def.Code, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String
            && v.GetString() == "hidden treasure").Should().BeFalse("the archived value is hidden from the run");
        // 3. Palette: the archived field is not offered to the view builder.
        var palette = await buyer.GetFromJsonAsync<List<System.Text.Json.JsonElement>>("/api/views/fields?recordType=Requisition");
        palette!.Should().NotContain(p => p.GetProperty("fieldKey").GetString() == def.Code);
        // 4. Resolved form: the placement stays but the field does not render.
        var resolved = await buyer.GetFromJsonAsync<ResolvedFormDto>($"/api/entry-forms/resolve?recordType=Requisition&formId={form.Id}");
        resolved!.Fields.Should().NotContain(f => f.FieldKey == def.Code);
        // 5. Writes are rejected while archived.
        (await buyer.PutAsJsonAsync($"/api/custom-values/Requisition/{pr}",
            new { values = new Dictionary<string, string?> { [def.Code] = "smuggled" } }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        // 6. STORAGE untouched + the audit recorded the action.
        await fx.Factory.SeedAsync(db =>
        {
            db.CustomFieldValues.Count(v => v.FieldDefId == def.Id).Should().Be(1, "archive NEVER deletes — only hides");
            db.AuditEntries.Any(a => a.EntityId == def.Code && a.Action == "Archived").Should().BeTrue();
            return Task.CompletedTask;
        });

        // UN-ARCHIVE: everything comes back.
        (await admin.PostAsync($"/api/custom-fields/{def.Id}/unarchive", null)).EnsureSuccessStatusCode();
        (await buyer.GetFromJsonAsync<List<CustomValueDto>>($"/api/custom-values/Requisition/{pr}"))!
            .Should().Contain(v => v.Code == def.Code && v.Value == "hidden treasure", "reversible — the value reappears");
        (await buyer.GetFromJsonAsync<ResolvedFormDto>($"/api/entry-forms/resolve?recordType=Requisition&formId={form.Id}"))!
            .Fields.Should().Contain(f => f.FieldKey == def.Code, "the placement survived the archive round-trip");
        await fx.Factory.SeedAsync(db =>
        {
            db.AuditEntries.Any(a => a.EntityId == def.Code && a.Action == "Un-archived").Should().BeTrue();
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Archive_is_DISTINCT_from_remove_from_form_L6_keeps_the_value_visible_archive_hides_it_everywhere()
    {
        var suffix = S;
        var admin = fx.ClientAs("u_admin");
        var buyer = fx.ClientAs("u_faridah");
        var def = (await (await admin.PostAsJsonAsync("/api/custom-fields", new SaveCustomFieldDefRequest(
            $"T8 Contrast {suffix}", "Requisition", "Text", null, false, "", 0)))
            .Content.ReadFromJsonAsync<CustomFieldDefDto>())!;
        var form = (await (await admin.PostAsJsonAsync("/api/entry-forms", new SaveEntryFormRequest(
            $"T8 Form {suffix}", "Requisition", [EntryFormsFixture.Field("Department", 0), EntryFormsFixture.Field(def.Code, 1)])))
            .Content.ReadFromJsonAsync<EntryFormDefDto>())!;
        var pr = (await (await buyer.PostAsJsonAsync("/api/requisitions", EntryFormsFixture.Pr())).Content
            .ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
        (await buyer.PutAsJsonAsync($"/api/custom-values/Requisition/{pr}",
            new { values = new Dictionary<string, string?> { [def.Code] = "still here" } })).EnsureSuccessStatusCode();

        // L6 remove-from-form: the value STAYS readable on the record.
        (await admin.PutAsJsonAsync($"/api/entry-forms/{form.Id}",
            new SaveEntryFormRequest(form.Name, "Requisition", [EntryFormsFixture.Field("Department", 0)]))).EnsureSuccessStatusCode();
        (await buyer.GetFromJsonAsync<List<CustomValueDto>>($"/api/custom-values/Requisition/{pr}"))!
            .Should().Contain(v => v.Code == def.Code && v.Value == "still here",
                "remove-from-form is LAYOUT — the value survives on other surfaces (L6)");

        // Archive: NOW it is hidden everywhere.
        (await admin.PostAsync($"/api/custom-fields/{def.Id}/archive", null)).EnsureSuccessStatusCode();
        (await buyer.GetFromJsonAsync<List<CustomValueDto>>($"/api/custom-values/Requisition/{pr}"))!
            .Should().NotContain(v => v.Code == def.Code, "archive hides EVERY live surface — the distinction from L6");
    }
}
