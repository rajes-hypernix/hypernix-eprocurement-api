using FSH.Modules.Sourcing.Contracts.Dtos;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;

namespace Integration.Tests.Tests.Eprocure;

/// <summary>
/// Single-envelope path: no technical scoring — close → open commercial → award.
/// </summary>
[Collection(FshCollectionDefinition.Name)]
public sealed class SourcingSingleEnvelopeGoldenPathTests
{
    private readonly FshWebApplicationFactory _factory;
    private readonly AuthHelper _auth;

    public SourcingSingleEnvelopeGoldenPathTests(FshWebApplicationFactory factory)
    {
        _factory = factory;
        _auth = new AuthHelper(factory);
    }

    [Fact]
    public async Task SingleEnvelope_GoldenPath_Should_Award_WithoutTechnicalEval()
    {
        using var buyer = await _auth.CreateRootAdminClientAsync();
        var approver = await EprocureFlowHelper.CreateApproverAsync(_factory, buyer);
        using var approverClient = await _auth.CreateAuthenticatedClientAsync(approver.Email, approver.Password);

        var vendor = await EprocureFlowHelper.OnboardSwecVendorAsync(_factory, _auth, buyer, "sin");
        var (prId, rfqId, _) = await EprocureFlowHelper.CreateOpenRfqAsync(
            buyer, vendor.VendorId, envelope: "Single", memoPrefix: "Single");

        using var saveBid = await vendor.Client.PutAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/rfqs/{rfqId}/my-bid",
            new
            {
                lead = "Single Lead",
                warranty = "12m",
                lines = new[]
                {
                    new { itemCode = "ITEM-1", bidding = true, price = 88m, qty = 10m, partial = false, altItem = (string?)null }
                },
                answers = Array.Empty<object>(),
                files = Array.Empty<string>()
            });
        await EprocureFlowHelper.EnsureSuccessAsync(saveBid, "Save bid");

        using var submitBid = await vendor.Client.PostAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/rfqs/{rfqId}/my-bid/submit", new { });
        await EprocureFlowHelper.EnsureSuccessAsync(submitBid, "Submit bid");

        using var close = await buyer.PostAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/rfqs/{rfqId}/close", new { });
        await EprocureFlowHelper.EnsureSuccessAsync(close, "Close");

        // Dual would require tech open/finalize first; Single opens commercial immediately.
        using var openTech = await buyer.PostAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/rfqs/{rfqId}/technical-envelope/open", new { });
        openTech.StatusCode.ShouldBe(HttpStatusCode.Conflict,
            await openTech.Content.ReadAsStringAsync());

        using var openComm = await buyer.PostAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/rfqs/{rfqId}/commercial-envelope/open", new { });
        await EprocureFlowHelper.EnsureSuccessAsync(openComm, "Open commercial");

        using var submitAward = await buyer.PutAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/rfqs/{rfqId}/award",
            new
            {
                allocations = new[]
                {
                    new { rfqLineCode = "L1", vendorId = vendor.VendorId, qty = 10m, unitPrice = 88m }
                }
            });
        await EprocureFlowHelper.EnsureSuccessAsync(submitAward, "Submit award");
        var pending = await submitAward.DeserializeAsync<AwardDto>();
        pending.Status.ShouldBe("PendingApproval");

        using var approve = await approverClient.PostAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/rfqs/{rfqId}/award/approve", new { });
        await EprocureFlowHelper.EnsureSuccessAsync(approve, "Approve award");
        var approved = await approve.DeserializeAsync<AwardDto>();
        approved.Status.ShouldBe("Approved");

        using var getRfq = await buyer.GetAsync($"{TestConstants.SourcingBasePath}/rfqs/{rfqId}");
        await EprocureFlowHelper.EnsureSuccessAsync(getRfq, "Get RFQ");
        var rfq = await getRfq.DeserializeAsync<RfqDetailDto>();
        rfq.Envelope.ShouldBe("Single");
        rfq.Status.ShouldBe("Awarded");

        using var getPr = await buyer.GetAsync($"{TestConstants.SourcingBasePath}/requisitions/{prId}");
        await EprocureFlowHelper.EnsureSuccessAsync(getPr, "Get PR");
        var pr = await getPr.DeserializeAsync<RequisitionDto>();
        pr.Lines[0].LifecycleStatus.ShouldBe("Awarded");
    }
}
