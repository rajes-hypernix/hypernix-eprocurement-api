using System.Net;
using System.Net.Http.Json;
using eProcure.Application.Forms;
using FluentAssertions;
using Xunit;

namespace eProcure.Tests.Integration;

/// <summary>
/// CF-FIX5-T8 — the customform_ (entry forms) and custseg_ (segments) prefix conventions,
/// consistent with custbody_/custcol_/CUSTLIST_. User keys the meaningful part; the system
/// guarantees the namespace. Existing ef_*/seg_* codes are grandfathered (create-path only,
/// no migration). The SAME custseg_ id serves a segment at header AND line (one dimension).
/// </summary>
public sealed class PrefixConventionTests(EntryFormsFixture fx) : IClassFixture<EntryFormsFixture>
{
    private static string S => Guid.NewGuid().ToString("N")[..6];

    [Fact]
    public async Task A_new_entry_form_takes_the_customform_prefix_from_the_user_keyed_id()
    {
        var s = S;
        var form = (await (await fx.ClientAs("u_admin").PostAsJsonAsync("/api/entry-forms",
            new SaveEntryFormRequest($"Prefix Form {s}", "Requisition", [EntryFormsFixture.Field("Department", 0)], Code: $"project_{s}")))
            .Content.ReadFromJsonAsync<EntryFormDefDto>())!;
        form.Code.Should().Be($"customform_project_{s}");

        // A prefix the user typed is stripped, never doubled.
        var form2 = (await (await fx.ClientAs("u_admin").PostAsJsonAsync("/api/entry-forms",
            new SaveEntryFormRequest($"Prefix Form2 {s}", "Requisition", [EntryFormsFixture.Field("Department", 0)], Code: $"customform_keyed_{s}")))
            .Content.ReadFromJsonAsync<EntryFormDefDto>())!;
        form2.Code.Should().Be($"customform_keyed_{s}");
    }

    [Fact]
    public async Task A_new_entry_form_with_no_id_auto_derives_customform_from_the_name()
    {
        var form = (await (await fx.ClientAs("u_admin").PostAsJsonAsync("/api/entry-forms",
            new SaveEntryFormRequest($"Auto Derived {S}", "Requisition", [EntryFormsFixture.Field("Department", 0)])))
            .Content.ReadFromJsonAsync<EntryFormDefDto>())!;
        form.Code.Should().StartWith("customform_auto");
    }

    [Fact]
    public async Task The_seeded_standard_forms_keep_their_grandfathered_ef_codes()
    {
        var forms = await fx.ClientAs("u_admin").GetFromJsonAsync<List<EntryFormDefDto>>("/api/entry-forms?recordType=Requisition");
        forms!.Single(f => f.IsSystem).Code.Should().Be("ef_standard_pr_form", "existing codes are immutable — no rename, no migration");
    }

    [Fact]
    public async Task A_new_segment_takes_the_custseg_prefix_and_the_seeded_system_segments_keep_seg()
    {
        var s = S;
        var seg = await (await fx.ClientAs("u_admin").PostAsJsonAsync("/api/segments",
            new { name = $"Prefix Seg {s}", hasHierarchy = false, required = false, code = $"region_{s}" }))
            .Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        seg.GetProperty("code").GetString().Should().Be($"custseg_region_{s}");

        // No id → derived from name, still custseg_.
        var seg2 = await (await fx.ClientAs("u_admin").PostAsJsonAsync("/api/segments",
            new { name = $"Derived Seg {s}", hasHierarchy = false, required = false }))
            .Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        seg2.GetProperty("code").GetString().Should().StartWith("custseg_derived");

        // System segments (projection) keep their grandfathered seg_ codes.
        await fx.Factory.SeedAsync(db =>
        {
            db.SegmentDefs.Where(d => d.IsSystem).Select(d => d.Code).Should().OnlyContain(c => c.StartsWith("seg_"));
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task The_SAME_custseg_id_serves_the_segment_at_header_AND_line()
    {
        var s = S;
        var admin = fx.ClientAs("u_admin");
        var seg = await (await admin.PostAsJsonAsync("/api/segments",
            new { name = $"OneId Seg {s}", hasHierarchy = false, required = false, code = $"oneid_{s}" }))
            .Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var id = seg.GetProperty("id").GetGuid();
        var code = seg.GetProperty("code").GetString();
        code.Should().Be($"custseg_oneid_{s}");

        (await admin.PostAsJsonAsync($"/api/segments/{id}/applications", new { recordType = "PurchaseOrder", lineLevel = false })).EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync($"/api/segments/{id}/applications", new { recordType = "PurchaseOrder", lineLevel = true })).EnsureSuccessStatusCode();
        await fx.Factory.SeedAsync(db =>
        {
            // Both applications reference the one SegmentDef — one code, two levels.
            db.SegmentApplications.Where(a => a.SegmentDefId == id).Select(a => a.SegmentDefId).Distinct().Should().ContainSingle();
            db.SegmentDefs.Single(d => d.Id == id).Code.Should().Be(code);
            return Task.CompletedTask;
        });
        await admin.DeleteAsync($"/api/segments/{id}/applications/PurchaseOrder?line=true");
        await admin.DeleteAsync($"/api/segments/{id}/applications/PurchaseOrder?line=false");
        await admin.DeleteAsync($"/api/segments/{id}");
    }
}
