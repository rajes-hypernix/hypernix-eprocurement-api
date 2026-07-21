using FSH.Modules.Procurement.Contracts.Dtos;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;

namespace Integration.Tests.Tests.Eprocure;

/// <summary>Award → PO → ASN → GRN → Invoice golden path for Modules.Procurement.</summary>
[Collection(FshCollectionDefinition.Name)]
public sealed class ProcurementGoldenPathTests
{
    private readonly FshWebApplicationFactory _factory;
    private readonly AuthHelper _auth;

    public ProcurementGoldenPathTests(FshWebApplicationFactory factory)
    {
        _factory = factory;
        _auth = new AuthHelper(factory);
    }

    [Fact]
    public async Task AwardToInvoice_GoldenPath_Should_MatchPurchaseOrder()
    {
        using var buyer = await _auth.CreateRootAdminClientAsync();
        var approver = await EprocureFlowHelper.CreateApproverAsync(_factory, buyer);
        using var approverClient = await _auth.CreateAuthenticatedClientAsync(approver.Email, approver.Password);

        var vendor = await EprocureFlowHelper.OnboardSwecVendorAsync(_factory, _auth, buyer, "proc");
        var (prId, rfqId, _) = await EprocureFlowHelper.CreateOpenRfqAsync(
            buyer, vendor.VendorId, envelope: "Single", memoPrefix: "Proc");

        // Bid → close → commercial → award (Single envelope)
        using var saveBid = await vendor.Client.PutAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/rfqs/{rfqId}/my-bid",
            new
            {
                lead = "Proc Lead",
                warranty = "12m",
                lines = new[] { new { itemCode = "ITEM-1", bidding = true, price = 95m, qty = 10m, partial = false, altItem = (string?)null } },
                answers = Array.Empty<object>(),
                files = Array.Empty<string>()
            });
        await EprocureFlowHelper.EnsureSuccessAsync(saveBid, "Save bid");
        using var submitBid = await vendor.Client.PostAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/rfqs/{rfqId}/my-bid/submit", new { });
        await EprocureFlowHelper.EnsureSuccessAsync(submitBid, "Submit bid");
        using var close = await buyer.PostAsJsonAsync($"{TestConstants.SourcingBasePath}/rfqs/{rfqId}/close", new { });
        await EprocureFlowHelper.EnsureSuccessAsync(close, "Close");
        using var openComm = await buyer.PostAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/rfqs/{rfqId}/commercial-envelope/open", new { });
        await EprocureFlowHelper.EnsureSuccessAsync(openComm, "Open commercial");
        using var submitAward = await buyer.PutAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/rfqs/{rfqId}/award",
            new { allocations = new[] { new { rfqLineCode = "L1", vendorId = vendor.VendorId, qty = 10m, unitPrice = 95m } } });
        await EprocureFlowHelper.EnsureSuccessAsync(submitAward, "Submit award");
        using var approveAward = await approverClient.PostAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/rfqs/{rfqId}/award/approve", new { });
        await EprocureFlowHelper.EnsureSuccessAsync(approveAward, "Approve award");

        // Create PO from award
        using var createPos = await buyer.PostAsJsonAsync(
            $"{TestConstants.ProcurementBasePath}/purchase-orders/from-award",
            new { rfqId });
        await EprocureFlowHelper.EnsureSuccessAsync(createPos, "Create POs");
        var poIds = await createPos.DeserializeAsync<List<Guid>>();
        poIds.Count.ShouldBe(1);
        var poId = poIds[0];

        using var issue = await buyer.PostAsJsonAsync(
            $"{TestConstants.ProcurementBasePath}/purchase-orders/{poId}/issue", new { });
        await EprocureFlowHelper.EnsureSuccessAsync(issue, "Issue PO");

        using var ack = await vendor.Client.PostAsJsonAsync(
            $"{TestConstants.ProcurementBasePath}/purchase-orders/{poId}/acknowledge", new { });
        await EprocureFlowHelper.EnsureSuccessAsync(ack, "Acknowledge PO");

        using var asnResp = await vendor.Client.PostAsJsonAsync(
            $"{TestConstants.ProcurementBasePath}/asns",
            new
            {
                poId,
                carrier = "DHL",
                trackingNo = "TRK-1",
                shippedDate = (DateOnly?)null,
                expectedDate = (DateOnly?)null,
                lines = new[] { new { itemCode = "L1", shippedQty = 10m, lotNo = (string?)null } }
            });
        await EprocureFlowHelper.EnsureSuccessAsync(asnResp, "Create ASN");
        var asn = await asnResp.DeserializeAsync<AsnDto>();

        using var grnResp = await buyer.PostAsJsonAsync(
            $"{TestConstants.ProcurementBasePath}/grns/receive",
            new
            {
                asnId = asn.Id,
                lines = new[] { new { itemCode = "L1", receivedQty = 10m } }
            });
        await EprocureFlowHelper.EnsureSuccessAsync(grnResp, "Receive ASN");

        using var invResp = await vendor.Client.PostAsJsonAsync(
            $"{TestConstants.ProcurementBasePath}/invoices",
            new
            {
                poId,
                invoiceNo = "SUP-INV-1",
                date = DateOnly.FromDateTime(DateTime.UtcNow),
                whtRate = 0m,
                lines = new[] { new { itemCode = "L1", qty = 10m, unitPrice = 95m } }
            });
        await EprocureFlowHelper.EnsureSuccessAsync(invResp, "Submit invoice");
        var invoice = await invResp.DeserializeAsync<InvoiceDto>();
        invoice.Status.ShouldBe("Submitted");

        using var approveInv = await buyer.PostAsJsonAsync(
            $"{TestConstants.ProcurementBasePath}/invoices/{invoice.Id}/approve", new { });
        await EprocureFlowHelper.EnsureSuccessAsync(approveInv, "Approve invoice");
        var approved = await approveInv.DeserializeAsync<InvoiceDto>();
        approved.Status.ShouldBe("Approved");

        using var getPo = await buyer.GetAsync($"{TestConstants.ProcurementBasePath}/purchase-orders/{poId}");
        await EprocureFlowHelper.EnsureSuccessAsync(getPo, "Get PO");
        var po = await getPo.DeserializeAsync<PurchaseOrderDto>();
        po.Status.ShouldBe("Matched");
        po.Lines[0].ReceivedQty.ShouldBe(10m);
        po.Lines[0].InvoicedQty.ShouldBe(10m);

        _ = prId; // exercised via CreateOpenRfqAsync
    }
}
