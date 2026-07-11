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

public sealed class InvoiceServiceTests
{
    private sealed class NoopNs : INetSuiteClient
    {
        public Task PushPurchaseOrderAsync(string c, CancellationToken ct = default) => Task.CompletedTask;
        public Task PushVendorBillAsync(string c, CancellationToken ct = default) => Task.CompletedTask;
        public Task PushItemReceiptAsync(string c, CancellationToken ct = default) => Task.CompletedTask;
        public Task PushBillPaymentAsync(string c, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static async Task<(InvoiceService Svc, TestContext C, Guid PoId, Guid VendorId)> SetupAsync(
        decimal qty = 6, decimal received = 6, decimal poPrice = 22500)
    {
        var c = TestContext.New();
        var vendor = new Vendor { Code = "V-A", Name = "Alpha", RegisteredName = "Alpha" };
        c.Db.Vendors.Add(vendor);
        var po = new PurchaseOrder
        {
            Code = "PO-2026-1193", VendorId = vendor.Id, CreatedUtc = c.Clock.UtcNow, UpdatedUtc = c.Clock.UtcNow,
            Lines = [new PoLine { ItemCode = "MTR", Description = "Motor", Qty = qty, Uom = "Unit", UnitPrice = poPrice, ReceivedQty = received }],
        }.SeededAs(PoStatus.Received);
        c.Db.PurchaseOrders.Add(po);
        await c.Db.SaveChangesAsync();
        var svc = new InvoiceService(c.Db, c.Clock, c.Codes, c.Audit, new NoopNs(), c.User);
        return (svc, c, po.Id, vendor.Id);
    }

    private static SubmitInvoiceRequest Bill(decimal qty, decimal price, decimal wht = 0) =>
        new("SUP-INV-1", new DateOnly(2026, 6, 28), wht, [new CreateInvoiceLine("MTR", qty, price)]);

    [Fact]
    public async Task Submit_OverBill_IsCappedToBillable()
    {
        var (svc, c, poId, vendorId) = await SetupAsync(received: 6);
        c.User.Roles = [Roles.Vendor]; c.User.VendorId = vendorId;
        var inv = await svc.SubmitAsync(poId, Bill(qty: 999, price: 22500));   // billable = 6
        inv.Lines.Single().Qty.Should().Be(6);   // capped, not 999
    }

    [Fact]
    public async Task Submit_MatchedPrice_IsSubmittedAndPayableAfterApprove()
    {
        var (svc, c, poId, vendorId) = await SetupAsync();
        c.User.Roles = [Roles.Vendor]; c.User.VendorId = vendorId;
        var inv = await svc.SubmitAsync(poId, Bill(qty: 6, price: 22500));
        inv.Status.Should().Be("Submitted");
        inv.MatchStatus.Should().Be("Matched");

        c.User.Roles = [Roles.Buyer]; c.User.VendorId = null;
        var approved = await svc.ApproveAsync(inv.Id);
        approved.Status.Should().Be("Approved");
        approved.Payable.Should().BeTrue();
    }

    [Fact]
    public async Task Submit_PriceVarianceBeyondTolerance_FlipsToException()
    {
        var (svc, c, poId, vendorId) = await SetupAsync(poPrice: 22500);
        c.User.Roles = [Roles.Vendor]; c.User.VendorId = vendorId;
        // 23200 vs 22500 → 3.1% > 2% tolerance
        var inv = await svc.SubmitAsync(poId, Bill(qty: 6, price: 23200));
        inv.Status.Should().Be("Exception");
        inv.Payable.Should().BeFalse();
        inv.ExceptionReason.Should().NotBeNull();
    }

    [Fact]
    public async Task Approve_ExceptionInvoice_IsBlocked_UntilResolved()
    {
        var (svc, c, poId, vendorId) = await SetupAsync(poPrice: 22500);
        c.User.Roles = [Roles.Vendor]; c.User.VendorId = vendorId;
        var inv = await svc.SubmitAsync(poId, Bill(qty: 6, price: 23200));   // Exception

        c.User.Roles = [Roles.Buyer]; c.User.VendorId = null;
        var act = () => svc.ApproveAsync(inv.Id);
        await act.Should().ThrowAsync<DomainRuleException>().WithMessage("*Exception*cannot be approved*");

        // resolving the exception approves it
        var resolved = await svc.ResolveExceptionAsync(inv.Id);
        resolved.Status.Should().Be("Approved");
        resolved.Payable.Should().BeTrue();
    }

    [Fact]
    public async Task Totals_ComputeSstAndWhtServerSide()
    {
        var (svc, c, poId, vendorId) = await SetupAsync(qty: 6, received: 6, poPrice: 1000);
        c.User.Roles = [Roles.Vendor]; c.User.VendorId = vendorId;
        // subtotal = 6 * 1000 = 6000; SST 8% = 480; WHT 5% = 300; total = 6180
        var inv = await svc.SubmitAsync(poId, Bill(qty: 6, price: 1000, wht: 5));
        inv.Subtotal.Should().Be(6000);
        inv.Sst.Should().Be(480);
        inv.Wht.Should().Be(300);
        inv.Total.Should().Be(6180);
    }

    [Fact]
    public async Task Submit_ByForeignVendor_IsForbidden()
    {
        var (svc, c, poId, _) = await SetupAsync();
        c.User.Roles = [Roles.Vendor]; c.User.VendorId = Guid.NewGuid();
        var act = () => svc.SubmitAsync(poId, Bill(6, 22500));
        await act.Should().ThrowAsync<ForbiddenException>();
    }
}
