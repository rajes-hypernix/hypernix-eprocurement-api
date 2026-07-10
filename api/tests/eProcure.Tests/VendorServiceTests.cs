using eProcure.Application;
using eProcure.Application.Abstractions;
using eProcure.Application.Suppliers;
using eProcure.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace eProcure.Tests;

public sealed class VendorServiceTests
{
    private static VendorService NewService(out TestContext c)
    {
        c = TestContext.New();
        c.User.Roles = ["Buyer"];   // Buyer sees full bank details (Slice F masking)
        return new VendorService(c.Db, c.Clock, c.Codes, c.Audit, c.User);
    }

    [Fact]
    public async Task CreateAsync_GeneratesSequenceCode_PendingStatus_AndAudits()
    {
        var svc = NewService(out var c);
        var v = await svc.CreateAsync(new CreateVendorRequest("New Vendor Sdn Bhd", "Sarawak", "Sarawak", "Bintulu"));

        v.Code.Should().StartWith("SWK-V-2026-");
        v.Status.Should().Be("Pending");
        (await c.Db.AuditEntries.AnyAsync(a => a.EntityType == "Vendor" && a.Action == "Created"))
            .Should().BeTrue();
    }

    [Fact]
    public async Task ToggleStatusAsync_FlipsAndAudits()
    {
        var svc = NewService(out var c);
        var v = await svc.CreateAsync(new CreateVendorRequest("Acme", null, null, null));
        // created Pending; toggle -> Inactive ... toggle -> Registered
        var afterFirst = await svc.ToggleStatusAsync(v.Id);
        afterFirst.Status.Should().Be("Inactive");
        var afterSecond = await svc.ToggleStatusAsync(v.Id);
        afterSecond.Status.Should().Be("Registered");

        (await c.Db.AuditEntries.CountAsync(a => a.EntityType == "Vendor" && a.Action == "Status changed"))
            .Should().Be(2);
    }

    [Fact]
    public async Task SetCategoriesAsync_PersistsDistinctAndAudits()
    {
        var svc = NewService(out var c);
        var v = await svc.CreateAsync(new CreateVendorRequest("Acme", null, null, null));

        var updated = await svc.SetCategoriesAsync(v.Id,
            new SetCategoriesRequest(["40101800P", "40101800P", "39121000P"]));

        updated.Categories.Should().BeEquivalentTo(["40101800P", "39121000P"]);
        (await c.Db.AuditEntries.AnyAsync(a => a.Action == "Categories updated")).Should().BeTrue();
    }

    [Fact]
    public async Task ListAsync_FiltersByTypeAndQuery()
    {
        var svc = NewService(out _);
        await svc.CreateAsync(new CreateVendorRequest("Alpha Engineering", "Sarawak", null, null));
        await svc.CreateAsync(new CreateVendorRequest("Beta Trading", "Sabah", null, null));

        var byQuery = await svc.ListAsync(new VendorFilter("alpha", null, null));
        byQuery.Should().ContainSingle(v => v.Name == "Alpha Engineering");

        var byRegion = await svc.ListAsync(new VendorFilter(null, null, "Sabah"));
        byRegion.Should().ContainSingle(v => v.Name == "Beta Trading");
    }

    [Fact]
    public void VendorAccess_ForeignVendor_IsForbidden_OwnAndInternal_Allowed()
    {
        var vendorA = Guid.NewGuid();
        var vendorB = Guid.NewGuid();

        ICurrentUser vendorUser = new FakeCurrentUser("vu1", "Vendor A", [Domain.Identity.Roles.Vendor], vendorA);
        ICurrentUser internalUser = new FakeCurrentUser("u_faridah", "Faridah", [Domain.Identity.Roles.Buyer]);

        var foreign = () => VendorAccess.EnsureCanAccess(vendorUser, vendorB);
        foreign.Should().Throw<ForbiddenException>();

        VendorAccess.EnsureCanAccess(vendorUser, vendorA);  // own → ok
        VendorAccess.EnsureCanAccess(internalUser, vendorB); // internal → ok
    }
}
