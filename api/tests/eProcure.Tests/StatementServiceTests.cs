using eProcure.Application;
using eProcure.Domain.Identity;
using eProcure.Domain.Procurement;
using eProcure.Domain.Suppliers;
using eProcure.Infrastructure.Services;
using FluentAssertions;

namespace eProcure.Tests;

public sealed class StatementServiceTests
{
    private static async Task<(StatementService Svc, TestContext C, Guid VendorId)> SetupAsync()
    {
        var c = TestContext.New();
        var vendor = new Vendor { Code = "V-A", Name = "Alpha", RegisteredName = "Alpha" };
        c.Db.Vendors.Add(vendor);
        // PO received 4 of 4 @ 100 = 400 received value
        var po = new PurchaseOrder
        {
            Code = "PO-1", VendorId = vendor.Id, Status = PoStatus.Received, CreatedUtc = c.Clock.UtcNow, UpdatedUtc = c.Clock.UtcNow,
            Lines = [new PoLine { ItemCode = "X", Description = "X", Qty = 4, Uom = "Unit", UnitPrice = 100, ReceivedQty = 4, InvoicedQty = 4 }],
        };
        c.Db.PurchaseOrders.Add(po);
        // A Paid invoice (subtotal 400, SST 32 → total 432) settles the line
        c.Db.Invoices.Add(new Invoice
        {
            Code = "INV-1", PoId = po.Id, VendorId = vendor.Id, Date = "14/06/2026", Status = InvoiceStatus.Paid,
            Lines = [new InvoiceLine { ItemCode = "X", Description = "X", Qty = 4, Uom = "Unit", UnitPrice = 100 }],
            CreatedUtc = c.Clock.UtcNow, UpdatedUtc = c.Clock.UtcNow,
        });
        await c.Db.SaveChangesAsync();
        return (new StatementService(c.Db, c.Clock, c.User), c, vendor.Id);
    }

    [Fact]
    public async Task Summary_InvoicedPaidBalance_AreDerivedServerSide()
    {
        var (svc, c, vid) = await SetupAsync();
        c.User.Roles = [Roles.Buyer];
        var list = await svc.ListAsync();
        var s = list.Single(x => x.VendorId == vid);
        s.Invoiced.Should().Be(432);     // 400 + 8% SST
        s.Paid.Should().Be(432);         // invoice is Paid
        s.Balance.Should().Be(0);        // settled
        s.Grni.Should().Be(0);           // received value 400 fully invoiced (subtotal 400)
    }

    [Fact]
    public async Task Grni_ReceivedButNotInvoiced_AccruesPositive()
    {
        var (svc, c, vid) = await SetupAsync();
        // Add a PO received but with no invoice → GRNI accrual
        var po2 = new PurchaseOrder
        {
            Code = "PO-2", VendorId = vid, Status = PoStatus.PartiallyReceived, CreatedUtc = c.Clock.UtcNow, UpdatedUtc = c.Clock.UtcNow,
            Lines = [new PoLine { ItemCode = "Y", Description = "Y", Qty = 10, Uom = "Unit", UnitPrice = 50, ReceivedQty = 6 }],
        };
        c.Db.PurchaseOrders.Add(po2);
        await c.Db.SaveChangesAsync();

        c.User.Roles = [Roles.Buyer];
        var s = (await svc.ListAsync()).Single(x => x.VendorId == vid);
        s.Grni.Should().Be(300);   // 6 received * 50, none invoiced
    }

    [Fact]
    public async Task GetMine_ReturnsOwnStatement_VendorScoped()
    {
        var (svc, c, vid) = await SetupAsync();
        c.User.Roles = [Roles.Vendor]; c.User.VendorId = vid;
        var mine = await svc.GetMineAsync();
        mine!.VendorId.Should().Be(vid);
        mine.Ledger.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Get_ForeignVendor_IsForbidden()
    {
        var (svc, c, vid) = await SetupAsync();
        c.User.Roles = [Roles.Vendor]; c.User.VendorId = System.Guid.NewGuid();   // different vendor
        var act = () => svc.GetAsync(vid);
        await act.Should().ThrowAsync<ForbiddenException>();
    }
}
