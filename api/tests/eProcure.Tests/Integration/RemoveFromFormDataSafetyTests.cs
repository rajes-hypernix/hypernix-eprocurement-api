using System.Net.Http.Json;
using eProcure.Application.CustomFields;
using eProcure.Application.Forms;
using FluentAssertions;
using Xunit;

namespace eProcure.Tests.Integration;

/// <summary>
/// CF-FIX4-T3 — L6, THE DATA-SAFETY INVARIANT: removing a field from a form's layout
/// deletes ONLY its placement row (the L1 EntryFormField object). It NEVER touches the
/// field's stored VALUES: they stay on every record, stay readable, and still render on
/// any OTHER form that includes the field. Layout is not data. A "clean up on remove"
/// that cascades into value deletion is a SEVERE bug — this test is the guard. (To retire
/// a field's data, use the field LIFECYCLE — Inactivate / Archive / Purge — never the
/// form editor.)
/// </summary>
public sealed class RemoveFromFormDataSafetyTests(EntryFormsFixture fx) : IClassFixture<EntryFormsFixture>
{
    [Fact]
    public async Task Removing_a_field_from_a_form_deletes_the_placement_row_and_ZERO_value_rows()
    {
        var admin = fx.ClientAs("u_admin");

        // A custom field, placed on TWO forms, with a VALUE on a real PR.
        var def = (await (await admin.PostAsJsonAsync("/api/custom-fields", new SaveCustomFieldDefRequest(
            "L6 Probe", "Requisition", "Text", null, false, "", 0, Code: "l6_probe")))
            .Content.ReadFromJsonAsync<CustomFieldDefDto>())!;
        var formA = await fx.CreateForm("L6 Form A", EntryFormsFixture.Field("Department", 0),
            EntryFormsFixture.Field(def.Code, 1));
        var formB = await fx.CreateForm("L6 Form B", EntryFormsFixture.Field(def.Code, 0));

        var buyer = fx.ClientAs("u_faridah");
        var pr = (await (await buyer.PostAsJsonAsync("/api/requisitions", EntryFormsFixture.Pr())).Content
            .ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
        (await buyer.PutAsJsonAsync($"/api/custom-values/Requisition/{pr}",
            new { values = new Dictionary<string, string?> { [def.Code] = "precious value" } }))
            .EnsureSuccessStatusCode();

        await fx.Factory.SeedAsync(db =>
        {
            db.CustomFieldValues.Count(v => v.FieldDefId == def.Id).Should().Be(1);
            return Task.CompletedTask;
        });

        // THE REMOVE: save Form A WITHOUT the field — a pure layout operation.
        var without = formA.Fields.Where(f => f.FieldKey != def.Code).ToList();
        (await admin.PutAsJsonAsync($"/api/entry-forms/{formA.Id}",
            new SaveEntryFormRequest(formA.Name, "Requisition", without))).EnsureSuccessStatusCode();

        await fx.Factory.SeedAsync(db =>
        {
            // The placement row on Form A is gone; Form B's placement survives …
            db.EntryFormFields.Count(f => f.FormDefId == formA.Id && f.FieldKey == def.Code).Should().Be(0);
            db.EntryFormFields.Count(f => f.FormDefId == formB.Id && f.FieldKey == def.Code).Should().Be(1);
            // … and NOT ONE value row was touched (the invariant this file exists for).
            db.CustomFieldValues.Count(v => v.FieldDefId == def.Id).Should().Be(1,
                "remove-from-form is a LAYOUT op — it must never cascade into value deletion (L6)");
            return Task.CompletedTask;
        });

        // The value is still readable on the record and the field still renders elsewhere.
        var values = await buyer.GetFromJsonAsync<List<CustomValueDto>>($"/api/custom-values/Requisition/{pr}");
        values!.Single(v => v.Code == def.Code).Value.Should().Be("precious value");
    }
}
