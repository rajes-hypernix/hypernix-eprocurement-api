using System.Net;
using System.Net.Http.Json;
using eProcure.Application.Forms;
using FluentAssertions;
using Xunit;

namespace eProcure.Tests.Integration;

/// <summary>
/// CF-FIX4-T1 — the L3 Header invariant, pinned server-side (never UI-only):
/// every form ALWAYS has exactly one IsHeader group on the BODY; it cannot be deleted,
/// ever; it cannot move into a subtab; on non-system forms it may be RENAMED (the flag,
/// not the title, carries the invariant); system forms refuse all structure edits.
/// This is the guarantee that the T4 placement cascade can never reach an impossible
/// state — there is always a valid group to land a field in.
/// </summary>
public sealed class HeaderInvariantTests(EntryFormsFixture fx) : IClassFixture<EntryFormsFixture>
{
    [Fact]
    public async Task A_fresh_form_always_has_exactly_one_Header_group_even_when_no_field_names_it()
    {
        // Every field placed in a NON-Header group — the invariant group must still materialize.
        var form = await fx.CreateForm("Fix4 NoHeader", new EntryFormFieldDto(
            "Department", null, "Logistics", 0, "Normal", false, null, null, false, null, null));

        form.Groups.Should().ContainSingle(g => g.IsHeader,
            "a form can never persist without its Header group (L3)");
        var header = form.Groups.Single(g => g.IsHeader);
        header.Title.Should().Be("Header");
        header.SubtabId.Should().BeNull("Header lives on the body, always");
        form.Groups.Should().Contain(g => g.Title == "Logistics" && !g.IsHeader);
    }

    [Fact]
    public async Task The_Header_group_cannot_be_deleted_ever()
    {
        var form = await fx.CreateForm("Fix4 DelHeader", EntryFormsFixture.Field("Department", 0));
        var header = form.Groups.Single(g => g.IsHeader);

        var resp = await fx.ClientAs("u_admin").DeleteAsync($"/api/entry-forms/{form.Id}/groups/{header.Id}");
        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await resp.Content.ReadAsStringAsync()).Should().Contain("Header group cannot be deleted");

        // An ordinary EMPTY group still deletes — the guard is the flag, not a blanket freeze.
        var withGroup = await fx.ClientAs("u_admin").PostAsJsonAsync($"/api/entry-forms/{form.Id}/groups",
            new SaveGroupRequest("Disposable", null, 5, false));
        var dto = (await withGroup.Content.ReadFromJsonAsync<EntryFormDefDto>())!;
        var disposable = dto.Groups.Single(g => g.Title == "Disposable");
        (await fx.ClientAs("u_admin").DeleteAsync($"/api/entry-forms/{form.Id}/groups/{disposable.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Header_cannot_move_into_a_subtab_but_CAN_be_renamed_on_a_non_system_form()
    {
        var form = await fx.CreateForm("Fix4 MoveHeader", EntryFormsFixture.Field("Department", 0));
        var header = form.Groups.Single(g => g.IsHeader);
        var admin = fx.ClientAs("u_admin");
        var subtab = (await (await admin.PostAsJsonAsync($"/api/entry-forms/{form.Id}/subtabs",
            new SaveSubtabRequest("Extra", 0, false))).Content.ReadFromJsonAsync<EntryFormDefDto>())!
            .Subtabs.Single();

        // Into a subtab → refused (a subtab can hide; the landing zone cannot).
        (await admin.PutAsJsonAsync($"/api/entry-forms/{form.Id}/groups/{header.Id}",
            new SaveGroupRequest("Header", subtab.Id, 0, false)))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Rename on a NON-system form → allowed; IsHeader (the invariant) survives the title.
        var renamed = await admin.PutAsJsonAsync($"/api/entry-forms/{form.Id}/groups/{header.Id}",
            new SaveGroupRequest("Primary Information", null, 0, false));
        renamed.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = (await renamed.Content.ReadFromJsonAsync<EntryFormDefDto>())!;
        dto.Groups.Single(g => g.IsHeader).Title.Should().Be("Primary Information");
    }

    [Fact]
    public async Task System_forms_refuse_all_group_edits_including_the_Header_rename()
    {
        var forms = await fx.ClientAs("u_admin").GetFromJsonAsync<List<EntryFormDefDto>>("/api/entry-forms?recordType=Requisition");
        var standard = forms!.Single(f => f.IsSystem);
        var header = standard.Groups.Single(g => g.IsHeader);

        var resp = await fx.ClientAs("u_admin").PutAsJsonAsync($"/api/entry-forms/{standard.Id}/groups/{header.Id}",
            new SaveGroupRequest("Renamed", null, 0, false));
        resp.StatusCode.Should().Be(HttpStatusCode.Conflict, "the standard form is the parity baseline — Header is not even renamable there");
    }

    [Fact]
    public async Task The_standard_PR_form_carries_the_IsHeader_flag_from_seed()
    {
        var forms = await fx.ClientAs("u_admin").GetFromJsonAsync<List<EntryFormDefDto>>("/api/entry-forms?recordType=Requisition");
        forms!.Single(f => f.IsSystem).Groups.Should().ContainSingle(g => g.IsHeader && g.Title == "Header" && g.SubtabId == null);
    }
}
