using System.Net;
using System.Net.Http.Json;
using eProcure.Application.CustomFields;
using eProcure.Application.Forms;
using FluentAssertions;
using Xunit;

namespace eProcure.Tests.Integration;

/// <summary>
/// CF-FIX4-T6 — the CF-FIX-3 lifecycle discipline applied to SEGMENTS: impact report
/// (providers looped, consumers named in output only), tiered delete, governed purge
/// (A73, transactional, snapshotted), fail-closed status via the SAME RecordLifecycle.
/// </summary>
public sealed class SegmentLifecycleTierTests(EntryFormsFixture fx) : IClassFixture<EntryFormsFixture>
{
    private static string S => Guid.NewGuid().ToString("N")[..6];

    private async Task<System.Text.Json.JsonElement> NewSegment(string name)
    {
        var resp = await fx.ClientAs("u_admin").PostAsJsonAsync("/api/segments",
            new { name, hasHierarchy = false, required = false });
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
    }

    [Fact]
    public async Task The_report_names_a_form_placement_and_a_view_reference_and_both_block_delete()
    {
        var admin = fx.ClientAs("u_admin");
        var seg = await NewSegment($"T6 Refs {S}");
        var segId = seg.GetProperty("id").GetGuid();
        var segCode = seg.GetProperty("code").GetString()!;
        await admin.PostAsJsonAsync($"/api/segments/{segId}/applications", new { recordType = "Requisition", lineLevel = false });

        // Place on a form (the L1 row) + reference from a view.
        var form = (await (await admin.PostAsJsonAsync("/api/entry-forms", new SaveEntryFormRequest(
            $"T6 Form {S}", "Requisition", [EntryFormsFixture.Field("Department", 0), EntryFormsFixture.Field(segCode, 1)])))
            .Content.ReadFromJsonAsync<EntryFormDefDto>())!;
        var buyer = fx.ClientAs("u_faridah");
        await buyer.PostAsJsonAsync("/api/views", new { name = $"t6-view-{S}", recordType = "Requisition",
            filters = Array.Empty<object>(), columns = new[] { new { fieldKey = segCode, label = (string?)null, sortDirection = (string?)null } } });

        var report = (await admin.GetFromJsonAsync<ImpactReportDto>($"/api/segments/{segId}/references"))!;
        report.ConfigReferences.Should().Contain(r => r.ConsumerName == "Entry Forms" && r.TargetLabel == form.Name);
        report.ConfigReferences.Should().Contain(r => r.ConsumerName == "Saved Views");
        report.CanDelete.Should().BeFalse();

        (await admin.DeleteAsync($"/api/segments/{segId}")).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Historical_assignments_purge_transactionally_with_a_snapshot_live_never()
    {
        var admin = fx.ClientAs("u_admin");
        var seg = await NewSegment($"T6 Purge {S}");
        var segId = seg.GetProperty("id").GetGuid();
        var segCode = seg.GetProperty("code").GetString()!;
        await admin.PostAsJsonAsync($"/api/segments/{segId}/applications", new { recordType = "PurchaseOrder", lineLevel = false });
        var withValue = await (await admin.PostAsJsonAsync($"/api/segments/{segId}/values",
            new { label = "Hist Site", parentValueId = (Guid?)null, sort = 0 })).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var valueId = withValue.GetProperty("values").EnumerateArray().First().GetProperty("id").GetGuid();
        var valueCode = withValue.GetProperty("values").EnumerateArray().First().GetProperty("code").GetString();

        Guid closedPo = default, draftPo = default;
        await fx.Factory.SeedAsync(db =>
        {
            var closed = new eProcure.Domain.Procurement.PurchaseOrder { Code = $"PO-T6H-{S}", VendorId = Guid.NewGuid(), CreatedUtc = DateTime.UtcNow, UpdatedUtc = DateTime.UtcNow }.SeededAs(eProcure.Domain.Procurement.PoStatus.Closed);
            var draft = new eProcure.Domain.Procurement.PurchaseOrder { Code = $"PO-T6L-{S}", VendorId = Guid.NewGuid(), CreatedUtc = DateTime.UtcNow, UpdatedUtc = DateTime.UtcNow };
            db.PurchaseOrders.AddRange(closed, draft);
            closedPo = closed.Id; draftPo = draft.Id;
            db.SegmentAssignments.AddRange(
                new eProcure.Domain.Segments.SegmentAssignment { SegmentDefId = segId, SegmentValueId = valueId, RecordType = eProcure.Domain.Views.RecordType.PurchaseOrder, RecordId = closed.Id, UpdatedUtc = DateTime.UtcNow },
                new eProcure.Domain.Segments.SegmentAssignment { SegmentDefId = segId, SegmentValueId = valueId, RecordType = eProcure.Domain.Views.RecordType.PurchaseOrder, RecordId = draft.Id, UpdatedUtc = DateTime.UtcNow });
            return Task.CompletedTask;
        });

        // LIVE assignment blocks delete AND purge (fail-closed lifecycle — the CF-FIX-3 allowlist).
        (await admin.DeleteAsync($"/api/segments/{segId}")).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await admin.PostAsync($"/api/segments/{segId}/purge", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Clear the LIVE one → historical remains → purge (A73) removes it with a snapshot.
        await fx.Factory.SeedAsync(db =>
        {
            db.SegmentAssignments.Remove(db.SegmentAssignments.Single(a => a.RecordId == draftPo));
            return Task.CompletedTask;
        });
        var report = (await admin.GetFromJsonAsync<ImpactReportDto>($"/api/segments/{segId}/references"))!;
        report.CanPurge.Should().BeTrue();
        report.CanDelete.Should().BeFalse("historical assignments block plain delete — purge is the governed path");

        // A Buyer holds ManageSegments? No — and even Admin-only A73 gates the purge tier.
        (await fx.ClientAs("u_faridah").PostAsync($"/api/segments/{segId}/purge", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.PostAsync($"/api/segments/{segId}/purge", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await fx.Factory.SeedAsync(db =>
        {
            db.SegmentDefs.Any(d => d.Id == segId).Should().BeFalse("purge deletes the def after removing history");
            db.SegmentAssignments.Any(a => a.SegmentDefId == segId).Should().BeFalse();
            db.AuditEntries.Any(a => a.EntityType == "Segment" && a.Action == "Purged historical assignment"
                && a.After != null && a.After.Contains(valueCode!)).Should().BeTrue("every removed assignment is snapshotted");
            db.PurchaseOrders.Remove(db.PurchaseOrders.Single(p => p.Id == closedPo));
            db.PurchaseOrders.Remove(db.PurchaseOrders.Single(p => p.Id == draftPo));
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task A_view_filter_VALUE_blocks_that_segment_values_delete()
    {
        var admin = fx.ClientAs("u_admin");
        var seg = await NewSegment($"T6 ValRef {S}");
        var segId = seg.GetProperty("id").GetGuid();
        var segCode = seg.GetProperty("code").GetString()!;
        await admin.PostAsJsonAsync($"/api/segments/{segId}/applications", new { recordType = "Requisition", lineLevel = false });
        var withValue = await (await admin.PostAsJsonAsync($"/api/segments/{segId}/values",
            new { label = "Filtered Site", parentValueId = (Guid?)null, sort = 0 })).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var value = withValue.GetProperty("values").EnumerateArray().First();
        var valueId = value.GetProperty("id").GetGuid();
        var valueCode = value.GetProperty("code").GetString()!;

        var buyer = fx.ClientAs("u_faridah");
        (await buyer.PostAsJsonAsync("/api/views", new { name = $"t6-vf-{S}", recordType = "Requisition",
            filters = new[] { new { fieldKey = segCode, @operator = "Eq", value = valueCode, value2 = (string?)null } },
            columns = new[] { new { fieldKey = "Code", label = (string?)null, sortDirection = (string?)null } } })).EnsureSuccessStatusCode();

        var report = (await admin.GetFromJsonAsync<ImpactReportDto>($"/api/segments/values/{valueId}/references"))!;
        report.ConfigReferences.Should().ContainSingle(r => r.ConsumerName == "Saved Views",
            "a view filtering on THIS VALUE is a config reference at the value grain");
        (await admin.DeleteAsync($"/api/segments/{segId}/values/{valueId}")).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
