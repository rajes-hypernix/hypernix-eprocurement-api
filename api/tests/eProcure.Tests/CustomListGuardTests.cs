using eProcure.Application.Configuration;
using eProcure.Domain.Configuration;
using eProcure.Domain.CustomFields;
using eProcure.Domain.Suppliers;
using eProcure.Domain.Views;
using eProcure.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace eProcure.Tests;

/// <summary>
/// A2F-T3 (GAP-5, audit 2026-07-12): custom-list value delete carries the in-use guard —
/// the D5 custom-field-def discipline applied to list values. An UNREFERENCED value
/// hard-deletes; a REFERENCED one (custom-field ValueListCode, or a native code-holding
/// column the system list sources) DEACTIVATES: gone from new-entry options (which filter
/// Active), still resolving for display of records that hold its code. Zero migrations —
/// CustomListValue.Active predates this slice (Ruling 1); reactivation already exists via
/// UpdateValueAsync(active: true) and is pinned here.
/// </summary>
public sealed class CustomListGuardTests
{
    private static (TestContext C, CustomListService Svc) New()
    {
        var c = TestContext.New();
        return (c, new CustomListService(c.Db, c.Clock));
    }

    private static async Task<(CustomList List, CustomListValue Keep, CustomListValue Loose)> SeedList(
        TestContext c, string code = "COLOUR")
    {
        var list = new CustomList { Code = code, Name = code };
        var keep = new CustomListValue { Code = "RED", Label = "Red", Sort = 0 };
        var loose = new CustomListValue { Code = "BLUE", Label = "Blue", Sort = 1 };
        list.Values.AddRange([keep, loose]);
        c.Db.CustomLists.Add(list);
        await c.Db.SaveChangesAsync();
        return (list, keep, loose);
    }

    [Fact]
    public async Task Unreferenced_value_is_hard_deleted_and_gone_from_options()
    {
        var (c, svc) = New();
        var (_, _, loose) = await SeedList(c);

        var result = await svc.DeleteValueAsync(loose.Id);

        result.Should().BeNull("an unreferenced value hard-deletes — typo cleanup stays possible");
        (await c.Db.CustomListValues.AnyAsync(v => v.Id == loose.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Value_referenced_by_a_custom_field_deactivates_and_history_keeps_its_label()
    {
        var (c, svc) = New();
        var (list, keep, _) = await SeedList(c);
        var def = new CustomFieldDef
        {
            Code = "cf_colour", Label = "Colour", RecordType = RecordType.PurchaseOrder,
            DataType = CustomFieldDataType.ListValue, CustomListId = list.Id,
        };
        c.Db.CustomFieldDefs.Add(def);
        c.Db.CustomFieldValues.Add(new CustomFieldValue
        {
            FieldDefId = def.Id, RecordType = RecordType.PurchaseOrder, RecordId = Guid.NewGuid(),
            ValueListCode = "RED",
        });
        await c.Db.SaveChangesAsync();

        var result = await svc.DeleteValueAsync(keep.Id);

        result.Should().NotBeNull("a referenced value is never silently dropped");
        result!.Active.Should().BeFalse("it deactivates instead");
        var row = await c.Db.CustomListValues.SingleAsync(v => v.Id == keep.Id);
        row.Active.Should().BeFalse();
        row.Label.Should().Be("Red", "the stored code still resolves for display — history never degrades to a raw code");

        // Reactivation is the EXISTING update path (mirrored, not invented).
        var reactivated = await svc.UpdateValueAsync(keep.Id, new UpdateCustomListValueRequest("Red", null, 0, true));
        reactivated.Active.Should().BeTrue();
    }

    [Fact]
    public async Task Value_referenced_by_a_native_code_holding_column_deactivates()
    {
        var (c, svc) = New();
        var list = new CustomList { Code = "COUNTRY", Name = "Country", IsSystem = true };
        var my = new CustomListValue { Code = "MY", Label = "Malaysia", Sort = 0 };
        list.Values.Add(my);
        c.Db.CustomLists.Add(list);
        c.Db.Vendors.Add(new Vendor { Code = "V-1", Name = "V", RegisteredName = "V Sdn Bhd", Country = "MY" });
        await c.Db.SaveChangesAsync();

        var result = await svc.DeleteValueAsync(my.Id);

        result.Should().NotBeNull("Vendor.Country holds the ISO-2 code — the native probe catches it");
        result!.Active.Should().BeFalse();
        (await c.Db.CustomListValues.AnyAsync(v => v.Id == my.Id)).Should().BeTrue();
    }
}

/// <summary>CF1-T2: the list-SELF lifecycle — rename/description/order-mode edit, guarded
/// delete (the A2F-T3 value discipline at list grain), system-list protection.</summary>
public sealed class CustomListSelfLifecycleTests
{
    private static (TestContext C, CustomListService Svc) New()
    {
        var c = TestContext.New();
        return (c, new CustomListService(c.Db, c.Clock));
    }

    [Fact]
    public async Task Edit_list_renames_and_flips_order_mode()
    {
        var (c, svc) = New();
        var list = new CustomList { Code = "SIZES", Name = "Sizes" };
        c.Db.CustomLists.Add(list);
        await c.Db.SaveChangesAsync();

        var dto = await svc.UpdateListAsync("SIZES", new UpdateCustomListRequest("Garment Sizes", "sizing", "Alphabetical"));
        dto.Name.Should().Be("Garment Sizes");
        dto.OrderMode.Should().Be("Alphabetical");
        dto.Code.Should().Be("SIZES", "the code is immutable — fields are tagged to it");

        await FluentActions.Awaiting(() => svc.UpdateListAsync("SIZES", new UpdateCustomListRequest("X", null, "Randomly")))
            .Should().ThrowAsync<eProcure.Domain.DomainRuleException>("order mode is a closed set");
    }

    [Fact]
    public async Task Unreferenced_user_list_hard_deletes_and_referenced_list_deactivates()
    {
        var (c, svc) = New();
        var clean = new CustomList { Code = "CLEAN", Name = "Clean" };
        clean.Values.Add(new CustomListValue { Code = "A", Label = "A", Sort = 0 });
        var bound = new CustomList { Code = "BOUND", Name = "Bound" };
        c.Db.CustomLists.AddRange(clean, bound);
        await c.Db.SaveChangesAsync();
        c.Db.CustomFieldDefs.Add(new eProcure.Domain.CustomFields.CustomFieldDef
        {
            Code = "cf_b", Label = "B", RecordType = eProcure.Domain.Views.RecordType.PurchaseOrder,
            DataType = eProcure.Domain.CustomFields.CustomFieldDataType.ListValue, CustomListId = bound.Id,
        });
        await c.Db.SaveChangesAsync();

        (await svc.DeleteListAsync("CLEAN")).Should().BeNull("clean list hard-deletes, values with it");
        (await c.Db.CustomLists.AnyAsync(l => l.Code == "CLEAN")).Should().BeFalse();

        var kept = await svc.DeleteListAsync("BOUND");
        kept.Should().NotBeNull("a field def binds it — never silently dropped");
        kept!.Active.Should().BeFalse();
    }

    [Fact]
    public async Task System_lists_refuse_deactivation_and_deletion()
    {
        var (c, svc) = New();
        c.Db.CustomLists.Add(new CustomList { Code = "COUNTRY", Name = "Country", IsSystem = true });
        await c.Db.SaveChangesAsync();
        await FluentActions.Awaiting(() => svc.DeleteListAsync("COUNTRY"))
            .Should().ThrowAsync<eProcure.Domain.DomainRuleException>();
        await FluentActions.Awaiting(() => svc.SetListActiveAsync("COUNTRY", false))
            .Should().ThrowAsync<eProcure.Domain.DomainRuleException>();
    }
}

// CF-FIX1-T3: order-mode is choosable at CREATE (was edit-only) and applies immediately.
public sealed class CustomListCreateOrderModeTests
{
    // CF-FIX1-T6: blank code → auto-numbered 1,2,3… per list; explicit codes (seeds) untouched.
    [Fact]
    public async Task Values_auto_number_in_entry_order_and_explicit_codes_stay()
    {
        var c = TestContext.New();
        var svc = new CustomListService(c.Db, c.Clock);
        await svc.CreateListAsync(new("FIX1NUM", "Fix1 Numbers", null, null));
        (await svc.AddValueAsync("FIX1NUM", new(null, "First", null))).Code.Should().Be("1");
        (await svc.AddValueAsync("FIX1NUM", new("", "Second", null))).Code.Should().Be("2");
        (await svc.AddValueAsync("FIX1NUM", new("LEGACY", "Seeded-style", null))).Code.Should().Be("LEGACY");
        (await svc.AddValueAsync("FIX1NUM", new(null, "Third", null))).Code.Should().Be("3");
    }

    [Fact]
    public async Task Create_with_alphabetical_stores_the_mode_and_bad_modes_are_rejected()
    {
        var c = TestContext.New();
        var svc = new CustomListService(c.Db, c.Clock);
        var dto = await svc.CreateListAsync(new("FIX1AB", "Fix1 AB", null, null, "Alphabetical"));
        dto.OrderMode.Should().Be("Alphabetical");
        var bad = async () => await svc.CreateListAsync(new("FIX1BAD", "Fix1 Bad", null, null, "Randomly"));
        await bad.Should().ThrowAsync<eProcure.Domain.DomainRuleException>();
    }
}

// CF-FIX1-T9: the depends-on mechanism, hardened — every scenario the operator asked about.
public sealed class DependsOnHardeningTests
{
    private static (TestContext C, CustomListService Svc) New()
    {
        var c = TestContext.New();
        return (c, new CustomListService(c.Db, c.Clock));
    }

    [Fact]
    public async Task Dangling_parent_list_and_dangling_parent_value_are_rejected()
    {
        var (_, svc) = New();
        var badList = async () => await svc.CreateListAsync(new("T9ORPH", "T9 Orphan", null, "NO_SUCH_LIST"));
        await badList.Should().ThrowAsync<eProcure.Domain.DomainRuleException>();

        await svc.CreateListAsync(new("T9CTRY", "T9 Country", null, null));
        await svc.AddValueAsync("T9CTRY", new("MY", "Malaysia", null));
        await svc.CreateListAsync(new("T9STATE", "T9 State", null, "T9CTRY"));
        var badValue = async () => await svc.AddValueAsync("T9STATE", new("SGR", "Selangor", "XX"));
        await badValue.Should().ThrowAsync<eProcure.Domain.DomainRuleException>("the parent value must exist in the parent list");
        (await svc.AddValueAsync("T9STATE", new("SGR", "Selangor", "MY"))).ParentValueCode.Should().Be("MY");
    }

    [Fact]
    public async Task Deleting_a_parent_value_with_dependent_children_deactivates_instead_of_orphaning()
    {
        var (c, svc) = New();
        await svc.CreateListAsync(new("T9C2", "T9 Country2", null, null));
        var my = await svc.AddValueAsync("T9C2", new("MY", "Malaysia", null));
        await svc.CreateListAsync(new("T9S2", "T9 State2", null, "T9C2"));
        await svc.AddValueAsync("T9S2", new("SGR", "Selangor", "MY"));

        var result = await svc.DeleteValueAsync(my.Id);
        result.Should().NotBeNull("a parent with children DEACTIVATES (never-silently-orphan)");
        result!.Active.Should().BeFalse();
        c.Db.CustomListValues.Count(v => v.Code == "MY").Should().Be(1, "the row survives — Selangor still resolves its parent");
    }

    [Fact]
    public async Task List_level_reparenting_has_no_api_path_so_cycles_are_impossible_by_construction()
    {
        var (_, svc) = New();
        await svc.CreateListAsync(new("T9A", "T9 A", null, null));
        await svc.CreateListAsync(new("T9B", "T9 B", null, "T9A"));
        // UpdateCustomListRequest carries Name/Description/OrderMode ONLY — the compiler is
        // the guard: there is no way to point T9A at T9B after the fact.
        typeof(eProcure.Application.Configuration.UpdateCustomListRequest).GetProperty("ParentListCode")
            .Should().BeNull("reparenting is not in the update contract — cycles cannot be formed");
    }
}
