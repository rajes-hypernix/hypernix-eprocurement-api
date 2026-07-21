using FSH.Modules.Sourcing.Contracts.Dtos;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;

namespace Integration.Tests.Tests.Eprocure;

/// <summary>
/// End-to-end PR → RFQ → Bid → Evaluation → Award path for the migrated Sourcing module.
/// Uses root Admin for buyer/evaluator steps (Admin is granted every permission) and a
/// separate Approver user for segregation-of-duties on award approval.
/// </summary>
[Collection(FshCollectionDefinition.Name)]
public sealed class SourcingGoldenPathTests
{
    private readonly FshWebApplicationFactory _factory;
    private readonly AuthHelper _auth;

    public SourcingGoldenPathTests(FshWebApplicationFactory factory)
    {
        _factory = factory;
        _auth = new AuthHelper(factory);
    }

    [Fact]
    public async Task DualEnvelope_GoldenPath_Should_Award_And_SettlePrLines()
    {
        using var buyer = await _auth.CreateRootAdminClientAsync();
        var approver = await EprocureFlowHelper.CreateApproverAsync(_factory, buyer);
        using var approverClient = await _auth.CreateAuthenticatedClientAsync(approver.Email, approver.Password);

        var vendor = await EprocureFlowHelper.OnboardSwecVendorAsync(_factory, _auth, buyer, "bid");

        // ── PR ──────────────────────────────────────────────────────────
        using var createPr = await buyer.PostAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/requisitions",
            new
            {
                requestor = "Integration Buyer",
                department = "Ops",
                departmentCode = (string?)null,
                location = "HQ",
                locationCode = (string?)null,
                category = "IT",
                categoryCode = (string?)null,
                job = "J1",
                jobCode = (string?)null,
                memo = "Golden path PR",
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
        await EprocureFlowHelper.EnsureSuccessAsync(getPr, "Get PR");
        var pr = await getPr.DeserializeAsync<RequisitionDto>();
        pr.Lines.Count.ShouldBe(1);
        var prLine = pr.Lines[0];

        using var reserve = await buyer.PostAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/requisitions/{prId}/lines/{prLine.Id}/reserve",
            new { });
        await EprocureFlowHelper.EnsureSuccessAsync(reserve, "Reserve PR line");

        // ── RFQ draft + invite + release ─────────────────────────────────
        using var createRfq = await buyer.PostAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/rfqs",
            new
            {
                title = $"Golden RFQ {Guid.NewGuid():N}"[..24],
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

        var closesUtc = DateTime.UtcNow.AddDays(7);
        using var updateRfq = await buyer.PutAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/rfqs/{rfqId}",
            new
            {
                rfqId,
                title = $"Golden RFQ {rfqId:N}"[..24],
                envelope = "Dual",
                currency = "MYR",
                opensUtc = (DateTime?)null,
                closesUtc,
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
        await EprocureFlowHelper.EnsureSuccessAsync(updateRfq, "Update RFQ draft");

        using var invite = await buyer.PostAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/rfqs/{rfqId}/invitations",
            new { vendorId = vendor.VendorId });
        await EprocureFlowHelper.EnsureSuccessAsync(invite, "Invite vendor");

        using var release = await buyer.PostAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/rfqs/{rfqId}/release",
            new { });
        await EprocureFlowHelper.EnsureSuccessAsync(release, "Release RFQ");

        // ── Vendor bid ──────────────────────────────────────────────────
        using var myRfqs = await vendor.Client.GetAsync($"{TestConstants.SourcingBasePath}/my/rfqs");
        await EprocureFlowHelper.EnsureSuccessAsync(myRfqs, "List my RFQs");
        var invitations = await myRfqs.DeserializeAsync<List<MyInvitationDto>>();
        invitations.ShouldContain(i => i.RfqId == rfqId);

        using var saveBid = await vendor.Client.PutAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/rfqs/{rfqId}/my-bid",
            new
            {
                lead = "Jane Doe",
                warranty = "12m",
                lines = new[]
                {
                    new { itemCode = "ITEM-1", bidding = true, price = 95m, qty = 10m, partial = false, altItem = (string?)null }
                },
                answers = Array.Empty<object>(),
                files = Array.Empty<string>()
            });
        await EprocureFlowHelper.EnsureSuccessAsync(saveBid, "Save bid draft");

        using var submitBid = await vendor.Client.PostAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/rfqs/{rfqId}/my-bid/submit",
            new { });
        await EprocureFlowHelper.EnsureSuccessAsync(submitBid, "Submit bid");

        // ── Close + technical eval + commercial ─────────────────────────
        using var close = await buyer.PostAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/rfqs/{rfqId}/close",
            new { });
        await EprocureFlowHelper.EnsureSuccessAsync(close, "Close RFQ");

        using var openTech = await buyer.PostAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/rfqs/{rfqId}/technical-envelope/open",
            new { });
        await EprocureFlowHelper.EnsureSuccessAsync(openTech, "Open technical envelope");

        foreach (var criterion in new[] { "Compliance", "Experience", "Delivery", "QA" })
        {
            using var score = await buyer.PutAsJsonAsync(
                $"{TestConstants.SourcingBasePath}/rfqs/{rfqId}/technical-eval/scores",
                new { vendorId = vendor.VendorId, criterion, score = 90 });
            await EprocureFlowHelper.EnsureSuccessAsync(score, $"Score {criterion}");
        }

        using var finalize = await buyer.PostAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/rfqs/{rfqId}/technical-eval/finalize",
            new { });
        await EprocureFlowHelper.EnsureSuccessAsync(finalize, "Finalize technical");

        using var openComm = await buyer.PostAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/rfqs/{rfqId}/commercial-envelope/open",
            new { });
        await EprocureFlowHelper.EnsureSuccessAsync(openComm, "Open commercial envelope");

        // ── Award (submit as buyer, approve as different Approver) ───────
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
        var pendingAward = await submitAward.DeserializeAsync<AwardDto>();
        pendingAward.Status.ShouldBe("PendingApproval");

        using var approveAward = await approverClient.PostAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/rfqs/{rfqId}/award/approve",
            new { });
        await EprocureFlowHelper.EnsureSuccessAsync(approveAward, "Approve award");
        var approvedAward = await approveAward.DeserializeAsync<AwardDto>();
        approvedAward.Status.ShouldBe("Approved");

        using var getRfq = await buyer.GetAsync($"{TestConstants.SourcingBasePath}/rfqs/{rfqId}");
        await EprocureFlowHelper.EnsureSuccessAsync(getRfq, "Get RFQ after award");
        var rfq = await getRfq.DeserializeAsync<RfqDetailDto>();
        rfq.Status.ShouldBe("Awarded");

        using var getPrAfter = await buyer.GetAsync($"{TestConstants.SourcingBasePath}/requisitions/{prId}");
        await EprocureFlowHelper.EnsureSuccessAsync(getPrAfter, "Get PR after award");
        var prAfter = await getPrAfter.DeserializeAsync<RequisitionDto>();
        prAfter.Lines[0].LifecycleStatus.ShouldBe("Awarded");
    }
}
