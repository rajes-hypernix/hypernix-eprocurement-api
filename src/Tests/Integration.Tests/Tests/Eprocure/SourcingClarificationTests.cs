using FSH.Modules.Sourcing.Contracts.Dtos;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;

namespace Integration.Tests.Tests.Eprocure;

/// <summary>
/// Clarification threads: buyer publish-to-all, vendor reply, and cross-vendor isolation (404).
/// </summary>
[Collection(FshCollectionDefinition.Name)]
public sealed class SourcingClarificationTests
{
    private readonly FshWebApplicationFactory _factory;
    private readonly AuthHelper _auth;

    public SourcingClarificationTests(FshWebApplicationFactory factory)
    {
        _factory = factory;
        _auth = new AuthHelper(factory);
    }

    [Fact]
    public async Task PublishAndReply_Should_FanOut_And_IsolateThreads()
    {
        using var buyer = await _auth.CreateRootAdminClientAsync();
        var vendorA = await EprocureFlowHelper.OnboardSwecVendorAsync(_factory, _auth, buyer, "cla");
        var vendorB = await EprocureFlowHelper.OnboardSwecVendorAsync(_factory, _auth, buyer, "clb");

        var (_, _, rfqCode) = await EprocureFlowHelper.CreateOpenRfqAsync(
            buyer,
            [vendorA.VendorId, vendorB.VendorId],
            envelope: "Dual",
            memoPrefix: "Clarify");

        // Buyer publishes one question to all live invitees (scope = RFQ code).
        using var publish = await buyer.PostAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/clarifications",
            new
            {
                scope = rfqCode,
                vendorId = (Guid?)null,
                body = "Confirm delivery lead time in weeks.",
                published = true
            });
        await EprocureFlowHelper.EnsureSuccessAsync(publish, "Publish clarification");
        var published = await publish.DeserializeAsync<List<ClarificationMessageDto>>();
        published.Count.ShouldBe(2);
        published.ShouldAllBe(m => m.SenderKind == "Buyer" && m.Published && m.Scope == rfqCode);
        published.Select(m => m.VendorId).OrderBy(id => id).ShouldBe(
            new[] { vendorA.VendorId, vendorB.VendorId }.OrderBy(id => id));

        // Vendor A replies on their own thread.
        using var replyA = await vendorA.Client.PostAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/clarifications",
            new
            {
                scope = rfqCode,
                vendorId = (Guid?)null,
                body = "A: 4 weeks",
                published = false
            });
        await EprocureFlowHelper.EnsureSuccessAsync(replyA, "Vendor A reply");
        var aMessages = await replyA.DeserializeAsync<List<ClarificationMessageDto>>();
        aMessages.Count.ShouldBe(1);
        aMessages[0].SenderKind.ShouldBe("Vendor");
        aMessages[0].VendorId.ShouldBe(vendorA.VendorId);
        aMessages[0].Published.ShouldBeFalse();

        // Vendor A lists only their threads; Vendor B cannot open A's thread.
        using var listA = await vendorA.Client.GetAsync(
            $"{TestConstants.SourcingBasePath}/clarifications/threads");
        await EprocureFlowHelper.EnsureSuccessAsync(listA, "List threads A");
        var threadsA = await listA.DeserializeAsync<List<ClarificationThreadDto>>();
        threadsA.ShouldContain(t => t.Scope == rfqCode && t.VendorId == vendorA.VendorId);
        threadsA.ShouldNotContain(t => t.VendorId == vendorB.VendorId);

        using var peekB = await vendorB.Client.GetAsync(
            $"{TestConstants.SourcingBasePath}/clarifications/threads/{Uri.EscapeDataString(rfqCode)}/{vendorA.VendorId}");
        peekB.StatusCode.ShouldBe(HttpStatusCode.NotFound,
            await peekB.Content.ReadAsStringAsync());

        // Vendor A can read their own thread (buyer publish + own reply).
        using var threadA = await vendorA.Client.GetAsync(
            $"{TestConstants.SourcingBasePath}/clarifications/threads/{Uri.EscapeDataString(rfqCode)}/{vendorA.VendorId}");
        await EprocureFlowHelper.EnsureSuccessAsync(threadA, "Get thread A");
        var msgs = await threadA.DeserializeAsync<List<ClarificationMessageDto>>();
        msgs.Count.ShouldBeGreaterThanOrEqualTo(2);
        msgs.ShouldContain(m => m.SenderKind == "Buyer" && m.Published);
        msgs.ShouldContain(m => m.SenderKind == "Vendor" && m.Body.Contains("4 weeks"));
    }
}
