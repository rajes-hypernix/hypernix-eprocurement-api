using System.Net;
using System.Net.Http.Json;
using eProcure.Application.Forms;
using FluentAssertions;
using Xunit;

namespace eProcure.Tests.Integration;

/// <summary>
/// CF-FIX5-T7 — a segment applies to BOTH the header AND the line of a record type, using
/// the SAME dimension id (like NetSuite treats one dimension at two levels). The uniqueness
/// change (Def,RecordType) → (Def,RecordType,LineLevel) is additive: existing single-level
/// applications survive and a second level can be added alongside. Header/line reads are
/// independent (a header read never surfaces a line-only dimension, and vice versa).
/// </summary>
public sealed class SegmentBothLevelsTests(EntryFormsFixture fx) : IClassFixture<EntryFormsFixture>
{
    private static string S => Guid.NewGuid().ToString("N")[..6];

    private async Task<(System.Guid Id, string Code)> NewSegment(string name)
    {
        var el = await (await fx.ClientAs("u_admin").PostAsJsonAsync("/api/segments",
            new { name, hasHierarchy = false, required = false })).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        return (el.GetProperty("id").GetGuid(), el.GetProperty("code").GetString()!);
    }

    [Fact]
    public async Task Header_and_line_apply_COEXIST_on_the_same_record_type_with_the_same_id()
    {
        var admin = fx.ClientAs("u_admin");
        var (id, code) = await NewSegment($"T7 Both {S}");

        // PurchaseOrder is line-bearing. Apply header, then line — BOTH succeed.
        (await admin.PostAsJsonAsync($"/api/segments/{id}/applications", new { recordType = "PurchaseOrder", lineLevel = false })).EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync($"/api/segments/{id}/applications", new { recordType = "PurchaseOrder", lineLevel = true })).EnsureSuccessStatusCode();

        await fx.Factory.SeedAsync(db =>
        {
            var apps = db.SegmentApplications.Where(a => a.SegmentDefId == id && a.RecordType == eProcure.Domain.Views.RecordType.PurchaseOrder).ToList();
            apps.Should().HaveCount(2, "one header + one line application coexist");
            apps.Select(a => a.LineLevel).Should().BeEquivalentTo(new[] { false, true });
            // The SAME dimension id serves both — it is one SegmentDef, one code.
            db.SegmentDefs.Single(d => d.Id == id).Code.Should().Be(code);
            return Task.CompletedTask;
        });

        // Re-applying the SAME level is the only thing refused (the new per-level dup guard).
        (await admin.PostAsJsonAsync($"/api/segments/{id}/applications", new { recordType = "PurchaseOrder", lineLevel = false }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Unapply ONE level leaves the other standing.
        (await admin.DeleteAsync($"/api/segments/{id}/applications/PurchaseOrder?line=true")).EnsureSuccessStatusCode();
        await fx.Factory.SeedAsync(db =>
        {
            var apps = db.SegmentApplications.Where(a => a.SegmentDefId == id && a.RecordType == eProcure.Domain.Views.RecordType.PurchaseOrder).ToList();
            apps.Should().ContainSingle().Which.LineLevel.Should().BeFalse("only the line level was removed");
            return Task.CompletedTask;
        });
        await admin.DeleteAsync($"/api/segments/{id}/applications/PurchaseOrder?line=false");
        await admin.DeleteAsync($"/api/segments/{id}");
    }

    [Fact]
    public async Task An_EXISTING_single_level_application_survives_and_a_second_level_is_added_alongside()
    {
        // The migration-survival guarantee, exercised through the API: a header-only
        // application (the pre-CF-FIX5 shape) keeps working, and a line level slots in.
        var admin = fx.ClientAs("u_admin");
        var (id, _) = await NewSegment($"T7 Additive {S}");
        (await admin.PostAsJsonAsync($"/api/segments/{id}/applications", new { recordType = "Requisition", lineLevel = false })).EnsureSuccessStatusCode();

        // The header application still resolves the segment onto the record's header form path
        // (unchanged behaviour). Now add the LINE level ALONGSIDE — no conflict.
        (await admin.PostAsJsonAsync($"/api/segments/{id}/applications", new { recordType = "Requisition", lineLevel = true })).EnsureSuccessStatusCode();
        await fx.Factory.SeedAsync(db =>
        {
            db.SegmentApplications.Count(a => a.SegmentDefId == id && a.RecordType == eProcure.Domain.Views.RecordType.Requisition)
                .Should().Be(2, "the original header app survived and the line app was added alongside");
            return Task.CompletedTask;
        });
        await admin.DeleteAsync($"/api/segments/{id}/applications/Requisition?line=true");
        await admin.DeleteAsync($"/api/segments/{id}/applications/Requisition?line=false");
        await admin.DeleteAsync($"/api/segments/{id}");
    }

    [Fact]
    public async Task A_line_only_dimension_does_NOT_surface_on_the_header_read_and_vice_versa()
    {
        var admin = fx.ClientAs("u_admin");
        var buyer = fx.ClientAs("u_faridah");
        var (id, code) = await NewSegment($"T7 Split {S}");
        // Line-only application on PurchaseOrder.
        (await admin.PostAsJsonAsync($"/api/segments/{id}/applications", new { recordType = "PurchaseOrder", lineLevel = true })).EnsureSuccessStatusCode();

        // Seed a PO with one line (the InMemory fixture carries none).
        System.Guid poId = default, lineId = default;
        await fx.Factory.SeedAsync(db =>
        {
            var line = new eProcure.Domain.Procurement.PoLine { ItemCode = $"IT-{S}", Description = "d", Qty = 1, UnitPrice = 1 };
            var po = new eProcure.Domain.Procurement.PurchaseOrder { Code = $"PO-T7S-{S}", VendorId = System.Guid.NewGuid(), CreatedUtc = System.DateTime.UtcNow, UpdatedUtc = System.DateTime.UtcNow, Lines = { line } };
            db.PurchaseOrders.Add(po);
            poId = po.Id; lineId = line.Id;
            return Task.CompletedTask;
        });

        // Header read → the line-only dimension is ABSENT.
        var header = await buyer.GetFromJsonAsync<List<System.Text.Json.JsonElement>>($"/api/segment-assignments/PurchaseOrder/{poId}");
        header!.Should().NotContain(a => a.GetProperty("segmentCode").GetString() == code, "a line-only dimension never shows on the header");
        // Line read → it IS offered.
        var line = await buyer.GetFromJsonAsync<List<System.Text.Json.JsonElement>>($"/api/segment-assignments/PurchaseOrder/{poId}?lineId={lineId}");
        line!.Should().Contain(a => a.GetProperty("segmentCode").GetString() == code, "the line read offers the line dimension");

        await admin.DeleteAsync($"/api/segments/{id}/applications/PurchaseOrder?line=true");
        await admin.DeleteAsync($"/api/segments/{id}");
    }
}
