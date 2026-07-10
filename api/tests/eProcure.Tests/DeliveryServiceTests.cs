using eProcure.Application;
using eProcure.Application.Abstractions;
using eProcure.Application.Procurement;
using eProcure.Domain;
using eProcure.Domain.Identity;
using eProcure.Domain.Procurement;
using eProcure.Domain.Suppliers;
using eProcure.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace eProcure.Tests;

public sealed class DeliveryServiceTests
{
    private sealed class NoopNs : INetSuiteClient
    {
        public Task PushPurchaseOrderAsync(string c, CancellationToken ct = default) => Task.CompletedTask;
        public Task PushVendorBillAsync(string c, CancellationToken ct = default) => Task.CompletedTask;
        public Task PushItemReceiptAsync(string c, CancellationToken ct = default) => Task.CompletedTask;
        public Task PushBillPaymentAsync(string c, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static async Task<(DeliveryService Svc, TestContext C, Guid PoId, Guid VendorId)> SetupAsync(
        decimal qty, decimal received, PoStatus status = PoStatus.Acknowledged)
    {
        var c = TestContext.New();
        var vendor = new Vendor { Code = "V-A", Name = "Alpha", RegisteredName = "Alpha" };
        c.Db.Vendors.Add(vendor);
        var po = new PurchaseOrder
        {
            Code = "PO-2026-1190", VendorId = vendor.Id, CreatedUtc = c.Clock.UtcNow, UpdatedUtc = c.Clock.UtcNow,
            Lines = [new PoLine { ItemCode = "ITEM", Description = "Item", Qty = qty, Uom = "Unit", UnitPrice = 100, ReceivedQty = received }],
        }.SeededAs(status);
        c.Db.PurchaseOrders.Add(po);
        await c.Db.SaveChangesAsync();
        var svc = new DeliveryService(c.Db, c.Clock, c.Codes, c.Audit, new NoopNs(), c.User);
        return (svc, c, po.Id, vendor.Id);
    }

    private static CreateAsnRequest Ship(string item, decimal qty) =>
        new("Carrier", "TRK", "28/06/2026", "29/06/2026", [new CreateAsnLine(item, qty, "LOT")]);

    [Fact]
    public async Task CreateAsn_OverShip_IsClampedToRemaining()
    {
        // PO qty 1, nothing received → remaining 1; try to ship 99
        var (svc, c, poId, vendorId) = await SetupAsync(qty: 1, received: 0);
        c.User.Roles = [Roles.Vendor]; c.User.VendorId = vendorId;
        var asn = await svc.CreateAsnAsync(poId, Ship("ITEM", 99));
        asn.Lines.Single().ShippedQty.Should().Be(1);   // clamped to remaining, not 99
    }

    [Fact]
    public async Task CreateAsn_WhenNothingLeftToShip_IsRejected_NoDuplicate()
    {
        var (svc, c, poId, vendorId) = await SetupAsync(qty: 1, received: 0);
        c.User.Roles = [Roles.Vendor]; c.User.VendorId = vendorId;
        await svc.CreateAsnAsync(poId, Ship("ITEM", 1));   // ships the only unit (in transit)
        var act = () => svc.CreateAsnAsync(poId, Ship("ITEM", 1));
        await act.Should().ThrowAsync<DomainRuleException>().WithMessage("*Nothing left to ship*");
    }

    [Fact]
    public async Task Receive_OverReceipt_IsCappedToShippedAndOutstanding()
    {
        var (svc, c, poId, vendorId) = await SetupAsync(qty: 1, received: 0);
        c.User.Roles = [Roles.Vendor]; c.User.VendorId = vendorId;
        var asn = await svc.CreateAsnAsync(poId, Ship("ITEM", 1));   // shipped 1

        c.User.Roles = [Roles.Buyer]; c.User.VendorId = null;
        await svc.ReceiveAsync(asn.Id, new ReceiveRequest([new ReceiveLine("ITEM", 99, "Good")]));
        var po = await c.Db.PurchaseOrders.Include(p => p.Lines).FirstAsync(p => p.Id == poId);
        po.Lines.Single().ReceivedQty.Should().Be(1);   // capped to shipped(1), not 99
    }

    [Fact]
    public async Task Receive_UnderReceipt_FlagsShort_AndFreesShortfallForReshipment()
    {
        // PO qty 24, already received 16; ship 8 (in transit) → remaining 0; receive only 5
        var (svc, c, poId, vendorId) = await SetupAsync(qty: 24, received: 16, status: PoStatus.PartiallyReceived);
        c.User.Roles = [Roles.Vendor]; c.User.VendorId = vendorId;
        var asn = await svc.CreateAsnAsync(poId, Ship("ITEM", 8));

        c.User.Roles = [Roles.Buyer]; c.User.VendorId = null;
        var grn = await svc.ReceiveAsync(asn.Id, new ReceiveRequest([new ReceiveLine("ITEM", 5, "Good")]));
        grn.Lines.Single().Condition.Should().Be("Short");

        var po = await c.Db.PurchaseOrders.Include(p => p.Lines).FirstAsync(p => p.Id == poId);
        po.Lines.Single().ReceivedQty.Should().Be(21);  // 16 + 5

        // The 3-unit shortfall is freed: ASN is now Received (not in transit), so remaining = 24 - 0 - 21 = 3
        c.User.Roles = [Roles.Vendor]; c.User.VendorId = vendorId;
        var plan = await svc.GetShipPlanAsync(poId);
        plan.Lines.Single().Remaining.Should().Be(3);
    }

    [Fact]
    public async Task Receive_ByVendor_IsForbidden()
    {
        var (svc, c, poId, vendorId) = await SetupAsync(qty: 1, received: 0);
        c.User.Roles = [Roles.Vendor]; c.User.VendorId = vendorId;
        var asn = await svc.CreateAsnAsync(poId, Ship("ITEM", 1));
        var act = () => svc.ReceiveAsync(asn.Id, new ReceiveRequest([new ReceiveLine("ITEM", 1, "Good")]));
        await act.Should().ThrowAsync<ForbiddenException>();
    }
}
