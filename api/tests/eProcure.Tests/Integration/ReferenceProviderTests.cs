using eProcure.Application.CustomFields;
using eProcure.Domain.CustomFields;
using eProcure.Domain.Procurement;
using eProcure.Domain.Views;
using eProcure.Infrastructure.Referencing;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace eProcure.Tests.Integration;

/// <summary>CF-FIX3-T1: each first-party provider against a seeded fixture, the
/// Live/Historical split, and the FAIL-CLOSED pins (operator additions).</summary>
public sealed class ReferenceProviderTests
{
    private static TestContext Ctx() => TestContext.New();

    private static CustomFieldDef Def(TestContext c, string code = "custbody_reftest")
    {
        var def = new CustomFieldDef { Code = code, Label = "Ref Test", DataType = CustomFieldDataType.Text, CreatedUtc = c.Clock.UtcNow, UpdatedUtc = c.Clock.UtcNow };
        c.Db.CustomFieldDefs.Add(def);
        c.Db.CustomFieldDefApplications.Add(new CustomFieldDefApplication { FieldDefId = def.Id, RecordType = RecordType.PurchaseOrder });
        return def;
    }

    [Fact]
    public async Task Entry_form_provider_reports_PER_FORM_grain_with_the_mandatory_flag()
    {
        var c = Ctx();
        var def = Def(c);
        var form = new eProcure.Domain.Forms.EntryFormDef { Id = Guid.NewGuid(), Code = "ef_one", Name = "First Form", RecordType = RecordType.Requisition, Active = true, CreatedUtc = c.Clock.UtcNow, UpdatedUtc = c.Clock.UtcNow };
        var form2 = new eProcure.Domain.Forms.EntryFormDef { Id = Guid.NewGuid(), Code = "ef_two", Name = "Second Form", RecordType = RecordType.Requisition, Active = true, CreatedUtc = c.Clock.UtcNow, UpdatedUtc = c.Clock.UtcNow };
        c.Db.EntryFormDefs.Add(form);
        var g1 = new eProcure.Domain.Forms.EntryFormGroup { Id = Guid.NewGuid(), FormDefId = form.Id, Title = "G", Sort = 9 };
        var g2 = new eProcure.Domain.Forms.EntryFormGroup { Id = Guid.NewGuid(), FormDefId = form2.Id, Title = "G", Sort = 0 };
        c.Db.EntryFormDefs.Add(form2); c.Db.EntryFormGroups.AddRange(g1, g2);
        c.Db.EntryFormFields.AddRange(
            new eProcure.Domain.Forms.EntryFormField { Id = Guid.NewGuid(), FormDefId = form.Id, FieldKey = def.Code, GroupId = g1.Id, Sort = 90, DisplayType = eProcure.Domain.Forms.EntryFormDisplayType.Normal, RequiredOnForm = true },
            new eProcure.Domain.Forms.EntryFormField { Id = Guid.NewGuid(), FormDefId = form2.Id, FieldKey = def.Code, GroupId = g2.Id, Sort = 0, DisplayType = eProcure.Domain.Forms.EntryFormDisplayType.Normal, RequiredOnForm = false });
        await c.Db.SaveChangesAsync();

        var refs = await new EntryFormReferenceProvider(c.Db).FindReferencesAsync(def.Id, def.Code);

        refs.Should().HaveCount(2, "PER-FORM grain — one reference per form, never one 'used in forms' fact");
        refs.Select(r => r.TargetId).Should().BeEquivalentTo([form.Id, form2.Id]);
        refs.Single(r => r.TargetId == form.Id).IsMandatory.Should().BeTrue("RequiredOnForm surfaces so the report can red-flag it");
        refs.All(r => r.ConsumerName == "Entry Forms").Should().BeTrue();
    }

    [Fact]
    public async Task Saved_view_provider_reports_columns_filters_and_filter_VALUES()
    {
        var c = Ctx();
        var def = Def(c);
        var list = new eProcure.Domain.Configuration.CustomList { Code = "REFLIST", Name = "Ref List", CreatedUtc = c.Clock.UtcNow, UpdatedUtc = c.Clock.UtcNow };
        list.Values.Add(new eProcure.Domain.Configuration.CustomListValue { Code = "V1", Label = "Value One" });
        c.Db.CustomLists.Add(list);
        var boundDef = new CustomFieldDef { Code = "custbody_bound", Label = "Bound", DataType = CustomFieldDataType.ListValue, CustomListId = list.Id, CreatedUtc = c.Clock.UtcNow, UpdatedUtc = c.Clock.UtcNow };
        c.Db.CustomFieldDefs.Add(boundDef);
        var view = new SavedView { Code = "VW-REF", Name = "Ref View", RecordType = RecordType.PurchaseOrder, IsShared = true, CreatedUtc = c.Clock.UtcNow, UpdatedUtc = c.Clock.UtcNow };
        view.Columns.Add(new SavedViewColumn { FieldKey = def.Code, Sort = 0 });
        view.Filters.Add(new SavedViewFilter { FieldKey = boundDef.Code, Operator = ViewOperator.Eq, Value = "V1", Sort = 0 });
        c.Db.SavedViews.Add(view);
        await c.Db.SaveChangesAsync();

        var p = new SavedViewReferenceProvider(c.Db);
        (await p.FindReferencesAsync(def.Id, def.Code)).Should().ContainSingle(r => r.Kind == FieldRefKind.ViewColumn && r.TargetLabel == "Ref View");
        (await p.FindReferencesAsync(list.Id, list.Code, "V1")).Should().ContainSingle(r => r.Kind == FieldRefKind.ViewFilter,
            "a list VALUE used as a filter criterion is a config reference");
    }

    [Fact]
    public async Task Data_provider_splits_a_Draft_PO_value_as_Live_and_a_Closed_PO_value_as_Historical()
    {
        var c = Ctx();
        var def = Def(c);
        var draftPo = new PurchaseOrder { Code = "PO-L", VendorId = Guid.NewGuid(), CreatedUtc = c.Clock.UtcNow, UpdatedUtc = c.Clock.UtcNow };
        var closedPo = new PurchaseOrder { Code = "PO-H", VendorId = Guid.NewGuid(), CreatedUtc = c.Clock.UtcNow, UpdatedUtc = c.Clock.UtcNow }.SeededAs(PoStatus.Closed);
        c.Db.PurchaseOrders.AddRange(draftPo, closedPo);
        c.Db.CustomFieldValues.AddRange(
            new CustomFieldValue { FieldDefId = def.Id, RecordType = RecordType.PurchaseOrder, RecordId = draftPo.Id, DataType = CustomFieldDataType.Text, ValueText = "live" },
            new CustomFieldValue { FieldDefId = def.Id, RecordType = RecordType.PurchaseOrder, RecordId = closedPo.Id, DataType = CustomFieldDataType.Text, ValueText = "old" });
        await c.Db.SaveChangesAsync();

        var summary = await new CustomValueDataProvider(c.Db).CountValuesAsync(def.Id);
        summary.LiveCount.Should().Be(1);
        summary.HistoricalCount.Should().Be(1);

        var snapshot = await new CustomValueDataProvider(c.Db).PurgeHistoricalAsync(def.Id);
        await c.Db.SaveChangesAsync();
        snapshot.Removed.Should().ContainSingle(v => v.Value == "old" && v.RecordLabel == "PO-H");
        (await c.Db.CustomFieldValues.CountAsync(v => v.FieldDefId == def.Id)).Should().Be(1, "the LIVE value is untouched");
    }

    // OPERATOR ADDITION (1): the FAIL-CLOSED default, pinned explicitly. A record the
    // classifier cannot map — here a value pointing at a record id that doesn't exist,
    // and a value on a NEVER-HISTORICAL type (Vendor) — must count as LIVE and must be
    // refused for purge. A future enum member can never silently become purgeable,
    // because historical requires an explicit allowlist HIT, never a fall-through.
    [Fact]
    public async Task Unknown_or_unmapped_status_is_LIVE_and_never_purged()
    {
        var c = Ctx();
        var def = Def(c);
        c.Db.CustomFieldDefApplications.Add(new CustomFieldDefApplication { FieldDefId = def.Id, RecordType = RecordType.Vendor });
        c.Db.CustomFieldValues.AddRange(
            // a dangling record id — the classifier finds nothing → LIVE (fail closed)
            new CustomFieldValue { FieldDefId = def.Id, RecordType = RecordType.PurchaseOrder, RecordId = Guid.NewGuid(), DataType = CustomFieldDataType.Text, ValueText = "ghost" },
            // a type with NO allowlist mapping at all (Vendor) → LIVE by construction
            new CustomFieldValue { FieldDefId = def.Id, RecordType = RecordType.Vendor, RecordId = Guid.NewGuid(), DataType = CustomFieldDataType.Text, ValueText = "master" });
        await c.Db.SaveChangesAsync();

        var summary = await new CustomValueDataProvider(c.Db).CountValuesAsync(def.Id);
        summary.LiveCount.Should().Be(2, "unknown/unmapped status counts as LIVE — fail closed");
        summary.HistoricalCount.Should().Be(0);

        var snapshot = await new CustomValueDataProvider(c.Db).PurgeHistoricalAsync(def.Id);
        snapshot.Removed.Should().BeEmpty("nothing that is not on the terminal-status allowlist is ever purged");
        (await c.Db.CustomFieldValues.CountAsync(v => v.FieldDefId == def.Id)).Should().Be(2);
    }
}
