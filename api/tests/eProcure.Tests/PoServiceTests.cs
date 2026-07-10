using eProcure.Application;
using eProcure.Application.Abstractions;
using eProcure.Domain;
using eProcure.Domain.Identity;
using eProcure.Domain.Procurement;
using eProcure.Domain.Suppliers;
using eProcure.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace eProcure.Tests;

public sealed class PoServiceTests
{
    private sealed class NoopNs : INetSuiteClient
    {
        public Task PushPurchaseOrderAsync(string c, CancellationToken ct = default) => Task.CompletedTask;
        public Task PushVendorBillAsync(string c, CancellationToken ct = default) => Task.CompletedTask;
        public Task PushItemReceiptAsync(string c, CancellationToken ct = default) => Task.CompletedTask;
        public Task PushBillPaymentAsync(string c, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static async Task<(PoService Svc, TestContext C, Guid PoId, Guid VendorId)> SetupAsync(PoStatus status)
    {
        var c = TestContext.New();
        c.User.Roles = [Roles.Buyer];
        var vendor = new Vendor { Code = "V-A", Name = "Alpha", RegisteredName = "Alpha" };
        c.Db.Vendors.Add(vendor);
        var po = new PurchaseOrder
        {
            Code = "PO-2026-1001", VendorId = vendor.Id, Status = status, CreatedUtc = c.Clock.UtcNow, UpdatedUtc = c.Clock.UtcNow,
            Lines = [new PoLine { ItemCode = "X", Description = "Item", Qty = 4, Uom = "Unit", UnitPrice = 100 }],
        };
        c.Db.PurchaseOrders.Add(po);
        await c.Db.SaveChangesAsync();
        return (new PoService(c.Db, c.Clock, c.Audit, new NoopNs(), c.User), c, po.Id, vendor.Id);
    }

    [Fact]
    public async Task Issue_DraftToIssued_SetsNsId_AndAudits()
    {
        var (svc, c, poId, _) = await SetupAsync(PoStatus.Draft);
        var po = await svc.IssueAsync(poId);
        po.Status.Should().Be("Issued");
        po.NsId.Should().StartWith("NS-PO-");
        (await c.Db.AuditEntries.AnyAsync(a => a.EntityType == "Po" && a.Action == "PO issued")).Should().BeTrue();
    }

    [Fact]
    public async Task Acknowledge_ByOwningVendor_IssuedToAcknowledged()
    {
        var (svc, c, poId, vendorId) = await SetupAsync(PoStatus.Issued);
        c.User.Roles = [Roles.Vendor]; c.User.VendorId = vendorId;
        var po = await svc.AcknowledgeAsync(poId);
        po.Status.Should().Be("Acknowledged");
        po.Acknowledged.Should().BeTrue();
    }

    [Fact]
    public async Task Acknowledge_ByForeignVendor_IsForbidden()
    {
        var (svc, c, poId, _) = await SetupAsync(PoStatus.Issued);
        c.User.Roles = [Roles.Vendor]; c.User.VendorId = Guid.NewGuid();  // different vendor
        var act = () => svc.AcknowledgeAsync(poId);
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task List_AsVendor_OnlyReturnsOwnPos()
    {
        var (svc, c, _, vendorId) = await SetupAsync(PoStatus.Issued);
        // add a second PO for another vendor
        var other = new Vendor { Code = "V-B", Name = "Beta", RegisteredName = "Beta" };
        c.Db.Vendors.Add(other);
        c.Db.PurchaseOrders.Add(new PurchaseOrder { Code = "PO-2026-1002", VendorId = other.Id, Status = PoStatus.Issued, CreatedUtc = c.Clock.UtcNow, UpdatedUtc = c.Clock.UtcNow });
        await c.Db.SaveChangesAsync();

        c.User.Roles = [Roles.Vendor]; c.User.VendorId = vendorId;
        var list = await svc.ListAsync();
        list.Should().OnlyContain(p => p.VendorId == vendorId);
    }

    [Fact]
    public async Task Issue_ByVendor_IsForbidden()
    {
        var (svc, c, poId, vendorId) = await SetupAsync(PoStatus.Draft);
        c.User.Roles = [Roles.Vendor]; c.User.VendorId = vendorId;
        var act = () => svc.IssueAsync(poId);
        await act.Should().ThrowAsync<ForbiddenException>();
    }
}
