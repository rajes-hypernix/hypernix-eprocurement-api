using FSH.Modules.Communication.Contracts;
using FSH.Modules.Notifications.Contracts.v1.DTOs;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;

namespace Integration.Tests.Tests.Eprocure;

/// <summary>RFQ release fans out an in-app inbox notification to the invited vendor.</summary>
[Collection(FshCollectionDefinition.Name)]
public sealed class CommunicationInboxSmokeTests
{
    private readonly FshWebApplicationFactory _factory;
    private readonly AuthHelper _auth;

    public CommunicationInboxSmokeTests(FshWebApplicationFactory factory)
    {
        _factory = factory;
        _auth = new AuthHelper(factory);
    }

    [Fact]
    public async Task ReleaseRfq_Should_NotifyInvitedVendorInbox()
    {
        using var buyer = await _auth.CreateRootAdminClientAsync();
        var vendor = await EprocureFlowHelper.OnboardSwecVendorAsync(_factory, _auth, buyer, "comm");
        var (_, rfqId, _) = await EprocureFlowHelper.CreateOpenRfqAsync(
            buyer, vendor.VendorId, envelope: "Single", memoPrefix: "CommNotify");

        using var unread = await vendor.Client.GetAsync($"{TestConstants.NotificationsBasePath}/unread-count");
        await EprocureFlowHelper.EnsureSuccessAsync(unread, "Unread count");
        var count = await unread.DeserializeAsync<int>();
        count.ShouldBeGreaterThan(0);

        using var list = await vendor.Client.GetAsync($"{TestConstants.NotificationsBasePath}/");
        await EprocureFlowHelper.EnsureSuccessAsync(list, "List notifications");
        var notifications = await list.DeserializeAsync<IReadOnlyList<NotificationDto>>();
        notifications.ShouldContain(n =>
            n.Type == CommunicationNotificationTypes.RfqReleased
            && n.Link != null
            && n.Link.Contains(rfqId.ToString(), StringComparison.Ordinal));
    }
}
