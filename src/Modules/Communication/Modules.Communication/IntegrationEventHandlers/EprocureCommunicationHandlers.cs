using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Eventing.Abstractions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Communication.Contracts;
using FSH.Modules.Communication.Contracts.Events;
using FSH.Modules.Notifications.Contracts.Services;
using FSH.Modules.Suppliers.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace FSH.Modules.Communication.IntegrationEventHandlers;

public sealed class OnboardingSubmittedIntegrationEventHandler(
    IInboxNotifier inbox,
    IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor,
    ILogger<OnboardingSubmittedIntegrationEventHandler> logger)
    : IIntegrationEventHandler<OnboardingSubmittedIntegrationEvent>
{
    public async Task HandleAsync(OnboardingSubmittedIntegrationEvent @event, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        EnsureTenant(@event.TenantId, tenantAccessor);

        await inbox.NotifyAsync(
            @event.BuyerUserId,
            CommunicationNotificationTypes.OnboardingSubmitted,
            "Onboarding application submitted",
            $"{@event.VendorName} submitted application {@event.ApplicationCode}.",
            $"/suppliers/onboarding/{@event.ApplicationId}",
            @event.Source,
            new { applicationId = @event.ApplicationId, applicationCode = @event.ApplicationCode },
            ct).ConfigureAwait(false);

        Log(logger, @event.BuyerUserId, @event.ApplicationId);
    }

    internal static void EnsureTenant(string? eventTenantId, IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor)
    {
        var ambient = tenantAccessor.MultiTenantContext.TenantInfo?.Id;
        if (!string.Equals(ambient, eventTenantId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Tenant context mismatch: ambient '{ambient ?? "(none)"}' != event '{eventTenantId ?? "(none)"}'.");
        }
    }

    private static void Log(ILogger logger, string userId, Guid applicationId)
    {
        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("Onboarding submitted notification for buyer {UserId} app {ApplicationId}", userId, applicationId);
    }
}

public sealed class OnboardingApprovedIntegrationEventHandler(
    IInboxNotifier inbox,
    IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor,
    ILogger<OnboardingApprovedIntegrationEventHandler> logger)
    : IIntegrationEventHandler<OnboardingApprovedIntegrationEvent>
{
    public async Task HandleAsync(OnboardingApprovedIntegrationEvent @event, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        OnboardingSubmittedIntegrationEventHandler.EnsureTenant(@event.TenantId, tenantAccessor);

        await inbox.NotifyAsync(
            @event.VendorUserId,
            CommunicationNotificationTypes.OnboardingApproved,
            "Vendor onboarding approved",
            $"Your vendor account {@event.VendorCode} is ready. Check your email to set a password.",
            "/vendor",
            @event.Source,
            new { vendorId = @event.VendorId, vendorCode = @event.VendorCode },
            ct).ConfigureAwait(false);

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("Onboarding approved notification for vendor user {UserId}", @event.VendorUserId);
    }
}

public sealed class RfqReleasedIntegrationEventHandler(
    IInboxNotifier inbox,
    IVendorPortalUserDirectory vendorUsers,
    IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor,
    ILogger<RfqReleasedIntegrationEventHandler> logger)
    : IIntegrationEventHandler<RfqReleasedIntegrationEvent>
{
    public async Task HandleAsync(RfqReleasedIntegrationEvent @event, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        OnboardingSubmittedIntegrationEventHandler.EnsureTenant(@event.TenantId, tenantAccessor);

        var userIds = await vendorUsers.GetIdentityUserIdsForVendorsAsync(@event.VendorIds, ct).ConfigureAwait(false);
        string title = string.IsNullOrWhiteSpace(@event.Title)
            ? $"RFQ {@event.RfqCode} is open for bidding"
            : $"RFQ {@event.RfqCode}: {@event.Title}";

        foreach (var userId in userIds)
        {
            await inbox.NotifyAsync(
                userId,
                CommunicationNotificationTypes.RfqReleased,
                title,
                "You have been invited to submit a bid.",
                $"/sourcing/rfqs/{@event.RfqId}",
                @event.Source,
                new { rfqId = @event.RfqId, rfqCode = @event.RfqCode },
                ct).ConfigureAwait(false);
        }

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("RFQ released notifications for {Count} users on {RfqCode}", userIds.Count, @event.RfqCode);
    }
}

public sealed class AwardApprovedIntegrationEventHandler(
    IInboxNotifier inbox,
    IVendorPortalUserDirectory vendorUsers,
    IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor,
    ILogger<AwardApprovedIntegrationEventHandler> logger)
    : IIntegrationEventHandler<AwardApprovedIntegrationEvent>
{
    public async Task HandleAsync(AwardApprovedIntegrationEvent @event, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        OnboardingSubmittedIntegrationEventHandler.EnsureTenant(@event.TenantId, tenantAccessor);

        var userIds = await vendorUsers.GetIdentityUserIdsForVendorsAsync(@event.VendorIds, ct).ConfigureAwait(false);
        foreach (var userId in userIds)
        {
            await inbox.NotifyAsync(
                userId,
                CommunicationNotificationTypes.AwardApproved,
                $"Award approved on RFQ {@event.RfqCode}",
                $"Award {@event.AwardCode} includes your allocation.",
                $"/sourcing/rfqs/{@event.RfqId}/award",
                @event.Source,
                new { rfqId = @event.RfqId, awardId = @event.AwardId, awardCode = @event.AwardCode },
                ct).ConfigureAwait(false);
        }

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("Award approved notifications for {Count} users on {AwardCode}", userIds.Count, @event.AwardCode);
    }
}
