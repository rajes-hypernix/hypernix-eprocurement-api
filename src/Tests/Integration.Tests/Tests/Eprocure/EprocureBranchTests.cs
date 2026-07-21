using FSH.Modules.Sourcing.Contracts.Dtos;
using FSH.Modules.Suppliers.Contracts.Dtos;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;

namespace Integration.Tests.Tests.Eprocure;

/// <summary>Critical branch paths beyond the golden path — reject, withdraw, cancel guards, rescind.</summary>
[Collection(FshCollectionDefinition.Name)]
public sealed class EprocureBranchTests
{
    private readonly FshWebApplicationFactory _factory;
    private readonly AuthHelper _auth;

    public EprocureBranchTests(FshWebApplicationFactory factory)
    {
        _factory = factory;
        _auth = new AuthHelper(factory);
    }

    [Fact]
    public async Task RejectOnboarding_Should_LeaveApplicationRejected_And_NoVendorLogin()
    {
        using var buyer = await _auth.CreateRootAdminClientAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var email = $"reject-{unique}@example.com";

        using var invite = await buyer.PostAsJsonAsync(
            $"{TestConstants.SuppliersBasePath}/onboarding/invitations",
            new { email, type = "Swec", selectedTemplateIds = (string[]?)null });
        await EprocureFlowHelper.EnsureSuccessAsync(invite, "Invite");
        var invitation = await invite.DeserializeAsync<OnboardingInvitationDto>();
        var token = EprocureFlowHelper.ExtractMagicLinkToken(invitation.MagicLink);

        using var anonymous = _factory.CreateClient();
        anonymous.DefaultRequestHeaders.Add("tenant", TestConstants.RootTenantId);

        using var resolve = await anonymous.PostAsJsonAsync(
            $"{TestConstants.SuppliersBasePath}/onboarding/resolve", new { token });
        var application = await resolve.DeserializeAsync<OnboardingApplicationDto>();

        using var draft = await anonymous.PutAsJsonAsync(
            $"{TestConstants.SuppliersBasePath}/onboarding/draft",
            new { token, name = $"Reject Co {unique}", registeredName = $"Reject Co {unique}", registrationNo = $"RJ-{unique}", email });
        await EprocureFlowHelper.EnsureSuccessAsync(draft, "Draft");

        using var submit = await anonymous.PostAsJsonAsync(
            $"{TestConstants.SuppliersBasePath}/onboarding/draft/submit", new { token });
        await EprocureFlowHelper.EnsureSuccessAsync(submit, "Submit");

        using var start = await buyer.PostAsJsonAsync(
            $"{TestConstants.SuppliersBasePath}/onboarding/applications/{application.Id}/start-review", new { });
        await EprocureFlowHelper.EnsureSuccessAsync(start, "Start review");

        using var reject = await buyer.PostAsJsonAsync(
            $"{TestConstants.SuppliersBasePath}/onboarding/applications/{application.Id}/reject",
            new { reason = "Incomplete documentation" });
        await EprocureFlowHelper.EnsureSuccessAsync(reject, "Reject");

        using var get = await buyer.GetAsync(
            $"{TestConstants.SuppliersBasePath}/onboarding/applications/{application.Id}");
        await EprocureFlowHelper.EnsureSuccessAsync(get, "Get application");
        var app = await get.DeserializeAsync<OnboardingApplicationDto>();
        app.Status.ShouldBe("Rejected");

        await Should.ThrowAsync<HttpRequestException>(async () =>
            await _auth.GetTokenAsync(email, EprocureFlowHelper.DefaultVendorPassword));
    }

    [Fact]
    public async Task WithdrawBid_Should_Allow_Resubmit()
    {
        using var buyer = await _auth.CreateRootAdminClientAsync();
        var vendor = await EprocureFlowHelper.OnboardSwecVendorAsync(_factory, _auth, buyer, "wd");
        var rfqId = await CreateOpenRfqAsync(buyer, vendor.VendorId);

        using var save = await vendor.Client.PutAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/rfqs/{rfqId}/my-bid",
            BidBody("Jane", 95m));
        await EprocureFlowHelper.EnsureSuccessAsync(save, "Save");

        using var submit = await vendor.Client.PostAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/rfqs/{rfqId}/my-bid/submit", new { });
        await EprocureFlowHelper.EnsureSuccessAsync(submit, "Submit");

        using var withdraw = await vendor.Client.PostAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/rfqs/{rfqId}/my-bid/withdraw", new { });
        await EprocureFlowHelper.EnsureSuccessAsync(withdraw, "Withdraw");

        using var saveAgain = await vendor.Client.PutAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/rfqs/{rfqId}/my-bid",
            BidBody("Jane 2", 90m));
        await EprocureFlowHelper.EnsureSuccessAsync(saveAgain, "Save after withdraw");

        using var resubmit = await vendor.Client.PostAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/rfqs/{rfqId}/my-bid/submit", new { });
        await EprocureFlowHelper.EnsureSuccessAsync(resubmit, "Resubmit");

        var bid = await resubmit.DeserializeAsync<BidDto>();
        bid.Submitted.ShouldBeTrue();
    }

    [Fact]
    public async Task CancelRequisition_Should_Conflict_When_LineInRfq()
    {
        using var buyer = await _auth.CreateRootAdminClientAsync();
        var vendor = await EprocureFlowHelper.OnboardSwecVendorAsync(_factory, _auth, buyer, "cx");
        var (prId, _) = await CreateOpenRfqReturningPrAsync(buyer, vendor.VendorId);

        using var cancel = await buyer.PostAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/requisitions/{prId}/cancel", new { });
        cancel.StatusCode.ShouldBe(HttpStatusCode.Conflict,
            await cancel.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task RescindInvitation_Should_Block_VendorBid()
    {
        using var buyer = await _auth.CreateRootAdminClientAsync();
        var vendor = await EprocureFlowHelper.OnboardSwecVendorAsync(_factory, _auth, buyer, "rs");
        var other = await EprocureFlowHelper.OnboardSwecVendorAsync(_factory, _auth, buyer, "rs2");

        var rfqId = await CreateDraftRfqWithInviteAsync(buyer, vendor.VendorId);

        using var rescind = await buyer.PostAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/rfqs/{rfqId}/invitations/{vendor.VendorId}/rescind",
            new { reasonCode = "OTHER", note = "changed mind" });
        await EprocureFlowHelper.EnsureSuccessAsync(rescind, "Rescind");

        using var inviteOther = await buyer.PostAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/rfqs/{rfqId}/invitations",
            new { vendorId = other.VendorId });
        await EprocureFlowHelper.EnsureSuccessAsync(inviteOther, "Invite other");

        using var release = await buyer.PostAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/rfqs/{rfqId}/release", new { });
        await EprocureFlowHelper.EnsureSuccessAsync(release, "Release");

        using var save = await vendor.Client.PutAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/rfqs/{rfqId}/my-bid",
            BidBody("Nope", 1m));
        save.StatusCode.ShouldBe(HttpStatusCode.NotFound,
            await save.Content.ReadAsStringAsync());
    }

    private static object BidBody(string lead, decimal price) => new
    {
        lead,
        warranty = "12m",
        lines = new[] { new { itemCode = "ITEM-1", bidding = true, price, qty = 10m, partial = false, altItem = (string?)null } },
        answers = Array.Empty<object>(),
        files = Array.Empty<string>()
    };

    private static async Task<Guid> CreateOpenRfqAsync(HttpClient buyer, Guid vendorId)
    {
        var (_, rfqId) = await CreateOpenRfqReturningPrAsync(buyer, vendorId);
        return rfqId;
    }

    private static async Task<(Guid PrId, Guid RfqId)> CreateOpenRfqReturningPrAsync(HttpClient buyer, Guid vendorId)
    {
        var (prId, rfqId) = await CreateDraftRfqWithInviteReturningPrAsync(buyer, vendorId);
        using var release = await buyer.PostAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/rfqs/{rfqId}/release", new { });
        await EprocureFlowHelper.EnsureSuccessAsync(release, "Release");
        return (prId, rfqId);
    }

    private static async Task<Guid> CreateDraftRfqWithInviteAsync(HttpClient buyer, Guid vendorId)
    {
        var (_, rfqId) = await CreateDraftRfqWithInviteReturningPrAsync(buyer, vendorId);
        return rfqId;
    }

    private static async Task<(Guid PrId, Guid RfqId)> CreateDraftRfqWithInviteReturningPrAsync(
        HttpClient buyer,
        Guid vendorId)
    {
        using var createPr = await buyer.PostAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/requisitions",
            new
            {
                requestor = "Branch Buyer",
                department = "Ops",
                departmentCode = (string?)null,
                location = "HQ",
                locationCode = (string?)null,
                category = "IT",
                categoryCode = (string?)null,
                job = "J1",
                jobCode = (string?)null,
                memo = "Branch PR",
                costCentre = "CC1",
                project = (string?)null,
                entryFormId = (Guid?)null,
                raisedOn = (DateOnly?)null,
                requiredOn = (DateOnly?)null,
                currency = "MYR",
                lines = new[] { new { itemCode = "ITEM-1", description = "Widget", qty = 10m, uom = "EA", estUnitPrice = 100m } },
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
                title = $"Branch RFQ {Guid.NewGuid():N}"[..24],
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

        using var update = await buyer.PutAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/rfqs/{rfqId}",
            new
            {
                rfqId,
                title = $"Branch RFQ {rfqId:N}"[..24],
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
        await EprocureFlowHelper.EnsureSuccessAsync(update, "Update RFQ");

        using var invite = await buyer.PostAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/rfqs/{rfqId}/invitations",
            new { vendorId });
        await EprocureFlowHelper.EnsureSuccessAsync(invite, "Invite");

        return (prId, rfqId);
    }
}
