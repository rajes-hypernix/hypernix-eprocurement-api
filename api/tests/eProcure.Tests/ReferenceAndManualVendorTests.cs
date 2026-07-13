using eProcure.Application.Suppliers;
using eProcure.Infrastructure.Seed;
using eProcure.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace eProcure.Tests;

/// <summary>
/// Bug 3 / Custom Lists — the manual New-Vendor form + the Custom List framework. The conformed
/// lists seed as data (list + values) and serve; a manual vendor saves to the master (Registered)
/// carrying the CODED dimension values analytics groups by (DATA-MODEL-ANALYTICS §4).
/// </summary>
public class ReferenceAndManualVendorTests
{
    [Fact]
    public async Task CustomLists_SeedAsData_WithDependentValues()
    {
        var ctx = TestContext.New();
        ctx.Db.CustomLists.AddRange(CustomListSeed.All(ctx.Clock.UtcNow));
        await ctx.Db.SaveChangesAsync();

        var lists = await new CustomListService(ctx.Db, ctx.Clock).ListAsync();

        var country = lists.Single(l => l.Code == "COUNTRY");
        country.Values.Should().Contain(v => v.Code == "MY" && v.Label == "Malaysia");
        lists.Single(l => l.Code == "STATE").Values.Count(v => v.ParentValueCode == "MY").Should().Be(16);
        lists.Single(l => l.Code == "CITY").Values.Should().Contain(v => v.Code == "Kuching" && v.ParentValueCode == "SWK");
        lists.Single(l => l.Code == "PAYMENT_TERMS").Values.Should().Contain(v => v.Code == "NET30" && v.Label == "30 days");
        lists.Single(l => l.Code == "BANK").Values.Should().Contain(v => v.Code == "MBB" && v.Label == "Maybank");
    }

    [Fact]
    public async Task CustomList_Admin_AddsAndEditsValues_WithoutCodeChange()
    {
        var ctx = TestContext.New();
        ctx.Db.CustomLists.AddRange(CustomListSeed.All(ctx.Clock.UtcNow));
        await ctx.Db.SaveChangesAsync();
        var svc = new CustomListService(ctx.Db, ctx.Clock);

        var added = await svc.AddValueAsync("BANK", new Application.Configuration.AddCustomListValueRequest("BSN", "Bank Simpanan Nasional", null));
        added.Code.Should().Be("BSN");
        (await svc.GetAsync("BANK"))!.Values.Should().Contain(v => v.Code == "BSN" && v.Label == "Bank Simpanan Nasional");

        // Edit the label + hide the value — the CODE is unchanged (stable conformed key).
        var updated = await svc.UpdateValueAsync(added.Id, new Application.Configuration.UpdateCustomListValueRequest("Bank Simpanan Nasional (BSN)", null, 99, false));
        updated.Code.Should().Be("BSN");
        updated.Label.Should().Be("Bank Simpanan Nasional (BSN)");
        updated.Active.Should().BeFalse();

        // Remove a value entirely.
        await svc.DeleteValueAsync(added.Id);
        (await svc.GetAsync("BANK"))!.Values.Should().NotContain(v => v.Code == "BSN");

        var dup = () => svc.AddValueAsync("BANK", new Application.Configuration.AddCustomListValueRequest("MBB", "dup", null));
        await dup.Should().ThrowAsync<eProcure.Domain.DomainRuleException>();   // conformed vocabulary: no dup codes
    }

    [Fact]
    public async Task CustomList_Admin_CreatesNewList_LikeNetSuiteCustomList()
    {
        var ctx = TestContext.New();
        var svc = new CustomListService(ctx.Db, ctx.Clock);

        var created = await svc.CreateListAsync(new Application.Configuration.CreateCustomListRequest("INCOTERM", "Incoterms", "Delivery terms", null));
        created.Code.Should().Be("CUSTLIST_INCOTERM");   // CF-FIX2-T1 contextual prefix
        created.IsSystem.Should().BeFalse();   // admin-created lists are not system lists

        await svc.AddValueAsync(created.Code, new Application.Configuration.AddCustomListValueRequest("FOB", "Free On Board", null));
        (await svc.GetAsync(created.Code))!.Values.Should().ContainSingle(v => v.Code == "FOB");

        var dupList = () => svc.CreateListAsync(new Application.Configuration.CreateCustomListRequest("INCOTERM", "dup", null, null));
        await dupList.Should().ThrowAsync<eProcure.Domain.DomainRuleException>();   // unique list code
    }

    [Fact]
    public async Task ManualVendor_SavesToMaster_WithCodedDimensions()
    {
        var ctx = TestContext.New();
        ctx.User.Roles = ["Buyer"];   // Buyer sees full bank details (Slice F masking)
        var svc = new VendorService(ctx.Db, ctx.Clock, ctx.Codes, ctx.Audit, ctx.User);

        var result = await svc.CreateManualAsync(new CreateManualVendorRequest(
            "KL Industrial Supplies", "1099282-K", "Non-SWEC", Region: null, State: "SGR", City: "Shah Alam",
            Country: "MY", Currency: "USD", PaymentTerms: "NET60", Bank: "CIMB", AccountNo: "8009-4421", Swift: "CIBBMYKL",
            ContactName: "Ali bin Ahmad", ContactEmail: "ali@kl.my", AddressLine: "Lot 5, Jalan Industri",
            Categories: ["E12", "E15"]));

        result.Vendor.Status.Should().Be("Registered");     // C1 — straight to master, no approval
        result.DuplicateWarning.Should().BeNull();

        var v = await ctx.Db.Vendors
            .Include(x => x.BankAccounts).Include(x => x.Contacts).Include(x => x.Addresses).Include(x => x.Currencies)
            .FirstAsync();
        v.Country.Should().Be("MY");                         // coded conformed dimensions (§4)
        v.State.Should().Be("SGR");
        v.City.Should().Be("Shah Alam");
        v.PaymentTerms.Should().Be("NET60");
        v.Currencies.Should().ContainSingle(c => c.Code == "USD" && c.IsPrimary);
        v.BankAccounts.Should().ContainSingle(b => b.Bank == "CIMB" && b.AccountNo == "8009-4421");
        v.Contacts.Should().ContainSingle(c => c.Name == "Ali bin Ahmad" && c.Email == "ali@kl.my");
        v.Addresses.Should().ContainSingle(a => a.City == "Shah Alam" && a.State == "SGR");
        v.Categories.Should().BeEquivalentTo(["E12", "E15"]);
    }
}
