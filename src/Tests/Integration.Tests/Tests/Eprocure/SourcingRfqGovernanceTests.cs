using FSH.Modules.Sourcing.Contracts.Dtos;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;

namespace Integration.Tests.Tests.Eprocure;

/// <summary>RFQ governance: extend keeps original close, early close, late-invite window.</summary>
[Collection(FshCollectionDefinition.Name)]
public sealed class SourcingRfqGovernanceTests
{
    private readonly FshWebApplicationFactory _factory;
    private readonly AuthHelper _auth;

    public SourcingRfqGovernanceTests(FshWebApplicationFactory factory)
    {
        _factory = factory;
        _auth = new AuthHelper(factory);
    }

    [Fact]
    public async Task Extend_Should_MoveCloses_And_PreserveOriginalCloses()
    {
        using var buyer = await _auth.CreateRootAdminClientAsync();
        var vendor = await EprocureFlowHelper.OnboardSwecVendorAsync(_factory, _auth, buyer, "ext");

        var originalCloses = DateTime.UtcNow.AddDays(5);
        var (_, rfqId, _) = await EprocureFlowHelper.CreateOpenRfqAsync(
            buyer, vendor.VendorId, memoPrefix: "Extend", closesUtc: originalCloses);

        using var before = await buyer.GetAsync($"{TestConstants.SourcingBasePath}/rfqs/{rfqId}");
        await EprocureFlowHelper.EnsureSuccessAsync(before, "Get before extend");
        var rfqBefore = await before.DeserializeAsync<RfqDetailDto>();
        rfqBefore.ExtensionCount.ShouldBe(0);
        rfqBefore.OriginalClosesUtc.ShouldNotBeNull();

        var newCloses = originalCloses.AddDays(3);
        using var extend = await buyer.PostAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/rfqs/{rfqId}/extend",
            new { newClosesUtc = newCloses, reasonCode = "MORE_TIME", note = "vendors asked" });
        await EprocureFlowHelper.EnsureSuccessAsync(extend, "Extend");

        using var after = await buyer.GetAsync($"{TestConstants.SourcingBasePath}/rfqs/{rfqId}");
        await EprocureFlowHelper.EnsureSuccessAsync(after, "Get after extend");
        var rfq = await after.DeserializeAsync<RfqDetailDto>();

        rfq.Status.ShouldBe("Open");
        rfq.ExtensionCount.ShouldBe(1);
        rfq.ClosesUtc.ShouldNotBeNull();
        rfq.ClosesUtc!.Value.ShouldBe(newCloses, TimeSpan.FromSeconds(2));
        rfq.OriginalClosesUtc!.Value.ShouldBe(rfqBefore.OriginalClosesUtc!.Value, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task CloseEarly_Should_SetClosed_WithoutChangingPlannedCloses()
    {
        using var buyer = await _auth.CreateRootAdminClientAsync();
        var vendor = await EprocureFlowHelper.OnboardSwecVendorAsync(_factory, _auth, buyer, "cls");

        var plannedCloses = DateTime.UtcNow.AddDays(7);
        var (_, rfqId, _) = await EprocureFlowHelper.CreateOpenRfqAsync(
            buyer, vendor.VendorId, memoPrefix: "Close", closesUtc: plannedCloses);

        using var before = await buyer.GetAsync($"{TestConstants.SourcingBasePath}/rfqs/{rfqId}");
        var rfqBefore = await before.DeserializeAsync<RfqDetailDto>();

        using var close = await buyer.PostAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/rfqs/{rfqId}/close", new { });
        await EprocureFlowHelper.EnsureSuccessAsync(close, "Close");

        using var after = await buyer.GetAsync($"{TestConstants.SourcingBasePath}/rfqs/{rfqId}");
        await EprocureFlowHelper.EnsureSuccessAsync(after, "Get after close");
        var rfq = await after.DeserializeAsync<RfqDetailDto>();

        rfq.Status.ShouldBe("Closed");
        rfq.ClosedUtc.ShouldNotBeNull();
        rfq.ClosesUtc!.Value.ShouldBe(rfqBefore.ClosesUtc!.Value, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task LateInvite_Should_Conflict_When_InsideMinRemainingWindow()
    {
        using var buyer = await _auth.CreateRootAdminClientAsync();
        var vendorA = await EprocureFlowHelper.OnboardSwecVendorAsync(_factory, _auth, buyer, "lia");
        var vendorB = await EprocureFlowHelper.OnboardSwecVendorAsync(_factory, _auth, buyer, "lib");

        // Default MinRemainingHoursForLateInvite = 72; close in 24h → late invite must fail.
        var (_, rfqId, _) = await EprocureFlowHelper.CreateOpenRfqAsync(
            buyer,
            vendorA.VendorId,
            memoPrefix: "Late",
            closesUtc: DateTime.UtcNow.AddHours(24));

        using var invite = await buyer.PostAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/rfqs/{rfqId}/invitations",
            new { vendorId = vendorB.VendorId });
        invite.StatusCode.ShouldBe(HttpStatusCode.Conflict,
            await invite.Content.ReadAsStringAsync());
    }
}
