using FSH.Modules.Sourcing.Contracts.Dtos;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;

namespace Integration.Tests.Tests.Eprocure;

/// <summary>Guard / auth edge cases for migrated Sourcing behaviour.</summary>
[Collection(FshCollectionDefinition.Name)]
public sealed class SourcingGuardTests
{
    private readonly FshWebApplicationFactory _factory;
    private readonly AuthHelper _auth;

    public SourcingGuardTests(FshWebApplicationFactory factory)
    {
        _factory = factory;
        _auth = new AuthHelper(factory);
    }

    [Fact]
    public async Task ApproveAward_Should_Reject_When_SameUserSubmitted()
    {
        using var buyer = await _auth.CreateRootAdminClientAsync();
        var vendor = await EprocureFlowHelper.OnboardSwecVendorAsync(_factory, _auth, buyer, "sod");
        var rfqId = await DriveToPendingAwardAsync(buyer, vendor);

        using var sameUserApprove = await buyer.PostAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/rfqs/{rfqId}/award/approve",
            new { });
        sameUserApprove.StatusCode.ShouldBe(HttpStatusCode.Conflict,
            $"Expected SoD rejection, got: {await sameUserApprove.Content.ReadAsStringAsync()}");
    }

    [Fact]
    public async Task SaveBid_Should_Return404_When_VendorNotInvited()
    {
        using var buyer = await _auth.CreateRootAdminClientAsync();
        var invited = await EprocureFlowHelper.OnboardSwecVendorAsync(_factory, _auth, buyer, "inv");
        var stranger = await EprocureFlowHelper.OnboardSwecVendorAsync(_factory, _auth, buyer, "str");

        var rfqId = await CreateOpenRfqWithInviteAsync(buyer, invited.VendorId);

        using var saveBid = await stranger.Client.PutAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/rfqs/{rfqId}/my-bid",
            new
            {
                lead = "Nope",
                warranty = "0",
                lines = new[]
                {
                    new { itemCode = "ITEM-1", bidding = true, price = 1m, qty = 1m, partial = false, altItem = (string?)null }
                },
                answers = Array.Empty<object>(),
                files = Array.Empty<string>()
            });

        saveBid.StatusCode.ShouldBe(HttpStatusCode.NotFound,
            $"Expected 404 for uninvited vendor, got: {await saveBid.Content.ReadAsStringAsync()}");
    }

    private static async Task<Guid> DriveToPendingAwardAsync(HttpClient buyer, OnboardedVendor vendor)
    {
        var rfqId = await CreateOpenRfqWithInviteAsync(buyer, vendor.VendorId);

        using var saveBid = await vendor.Client.PutAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/rfqs/{rfqId}/my-bid",
            new
            {
                lead = "Jane",
                warranty = "12m",
                lines = new[]
                {
                    new { itemCode = "ITEM-1", bidding = true, price = 95m, qty = 10m, partial = false, altItem = (string?)null }
                },
                answers = Array.Empty<object>(),
                files = Array.Empty<string>()
            });
        await EprocureFlowHelper.EnsureSuccessAsync(saveBid, "Save bid");

        using var submitBid = await vendor.Client.PostAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/rfqs/{rfqId}/my-bid/submit",
            new { });
        await EprocureFlowHelper.EnsureSuccessAsync(submitBid, "Submit bid");

        using var close = await buyer.PostAsJsonAsync($"{TestConstants.SourcingBasePath}/rfqs/{rfqId}/close", new { });
        await EprocureFlowHelper.EnsureSuccessAsync(close, "Close");

        using var openTech = await buyer.PostAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/rfqs/{rfqId}/technical-envelope/open", new { });
        await EprocureFlowHelper.EnsureSuccessAsync(openTech, "Open tech");

        foreach (var criterion in new[] { "Compliance", "Experience", "Delivery", "QA" })
        {
            using var score = await buyer.PutAsJsonAsync(
                $"{TestConstants.SourcingBasePath}/rfqs/{rfqId}/technical-eval/scores",
                new { vendorId = vendor.VendorId, criterion, score = 90 });
            await EprocureFlowHelper.EnsureSuccessAsync(score, $"Score {criterion}");
        }

        using var finalize = await buyer.PostAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/rfqs/{rfqId}/technical-eval/finalize", new { });
        await EprocureFlowHelper.EnsureSuccessAsync(finalize, "Finalize");

        using var openComm = await buyer.PostAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/rfqs/{rfqId}/commercial-envelope/open", new { });
        await EprocureFlowHelper.EnsureSuccessAsync(openComm, "Open commercial");

        using var submitAward = await buyer.PutAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/rfqs/{rfqId}/award",
            new
            {
                allocations = new[]
                {
                    new { rfqLineCode = "L1", vendorId = vendor.VendorId, qty = 10m, unitPrice = 95m }
                }
            });
        await EprocureFlowHelper.EnsureSuccessAsync(submitAward, "Submit award");
        return rfqId;
    }

    private static async Task<Guid> CreateOpenRfqWithInviteAsync(HttpClient buyer, Guid vendorId)
    {
        using var createPr = await buyer.PostAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/requisitions",
            new
            {
                requestor = "Guard Buyer",
                department = "Ops",
                departmentCode = (string?)null,
                location = "HQ",
                locationCode = (string?)null,
                category = "IT",
                categoryCode = (string?)null,
                job = "J1",
                jobCode = (string?)null,
                memo = "Guard PR",
                costCentre = "CC1",
                project = (string?)null,
                entryFormId = (Guid?)null,
                raisedOn = (DateOnly?)null,
                requiredOn = (DateOnly?)null,
                currency = "MYR",
                lines = new[]
                {
                    new { itemCode = "ITEM-1", description = "Widget", qty = 10m, uom = "EA", estUnitPrice = 100m }
                },
                submit = true
            });
        await EprocureFlowHelper.EnsureSuccessAsync(createPr, "Create PR");
        var prId = await createPr.DeserializeAsync<Guid>();

        using var getPr = await buyer.GetAsync($"{TestConstants.SourcingBasePath}/requisitions/{prId}");
        var pr = await getPr.DeserializeAsync<RequisitionDto>();
        var prLine = pr.Lines[0];

        using var reserve = await buyer.PostAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/requisitions/{prId}/lines/{prLine.Id}/reserve", new { });
        await EprocureFlowHelper.EnsureSuccessAsync(reserve, "Reserve");

        using var createRfq = await buyer.PostAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/rfqs",
            new
            {
                title = $"Guard RFQ {Guid.NewGuid():N}"[..24],
                envelope = "Dual",
                currency = "MYR",
                prRefs = new[] { pr.Code },
                lines = new[]
                {
                    new
                    {
                        lineCode = "L1",
                        itemCode = "ITEM-1",
                        description = "Widget",
                        qty = 10m,
                        uom = "EA",
                        prRef = pr.Code,
                        sourcePrLineIds = new[] { prLine.Id.ToString() }
                    }
                }
            });
        await EprocureFlowHelper.EnsureSuccessAsync(createRfq, "Create RFQ");
        var rfqId = await createRfq.DeserializeAsync<Guid>();

        using var updateRfq = await buyer.PutAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/rfqs/{rfqId}",
            new
            {
                rfqId,
                title = $"Guard RFQ {rfqId:N}"[..24],
                envelope = "Dual",
                currency = "MYR",
                opensUtc = (DateTime?)null,
                closesUtc = DateTime.UtcNow.AddDays(7),
                lines = new[]
                {
                    new
                    {
                        lineCode = "L1",
                        itemCode = "ITEM-1",
                        description = "Widget",
                        qty = 10m,
                        uom = "EA",
                        prRef = pr.Code,
                        sourcePrLineIds = new[] { prLine.Id.ToString() }
                    }
                },
                formItems = Array.Empty<object>(),
                technicalSections = Array.Empty<string>(),
                commercialSections = Array.Empty<string>(),
                technicalEvaluatorIds = Array.Empty<string>(),
                commercialEvaluatorIds = Array.Empty<string>()
            });
        await EprocureFlowHelper.EnsureSuccessAsync(updateRfq, "Update RFQ");

        using var invite = await buyer.PostAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/rfqs/{rfqId}/invitations",
            new { vendorId });
        await EprocureFlowHelper.EnsureSuccessAsync(invite, "Invite");

        using var release = await buyer.PostAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/rfqs/{rfqId}/release", new { });
        await EprocureFlowHelper.EnsureSuccessAsync(release, "Release");

        return rfqId;
    }
}
