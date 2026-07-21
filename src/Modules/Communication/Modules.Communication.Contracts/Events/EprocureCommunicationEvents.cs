using FSH.Framework.Eventing.Abstractions;

namespace FSH.Modules.Communication.Contracts.Events;

/// <summary>Vendor submitted an onboarding application — notify the inviting buyer.</summary>
public sealed record OnboardingSubmittedIntegrationEvent(
    Guid Id,
    DateTime OccurredOnUtc,
    string? TenantId,
    string CorrelationId,
    string Source,
    Guid ApplicationId,
    string ApplicationCode,
    string VendorName,
    string BuyerUserId) : IIntegrationEvent;

/// <summary>Onboarding approved and portal login provisioned — notify the vendor user.</summary>
public sealed record OnboardingApprovedIntegrationEvent(
    Guid Id,
    DateTime OccurredOnUtc,
    string? TenantId,
    string CorrelationId,
    string Source,
    Guid VendorId,
    string VendorCode,
    string VendorUserId) : IIntegrationEvent;

/// <summary>RFQ released — notify invited vendor portal users.</summary>
public sealed record RfqReleasedIntegrationEvent(
    Guid Id,
    DateTime OccurredOnUtc,
    string? TenantId,
    string CorrelationId,
    string Source,
    Guid RfqId,
    string RfqCode,
    string? Title,
    IReadOnlyList<Guid> VendorIds) : IIntegrationEvent;

/// <summary>Award approved — notify vendors that received allocations.</summary>
public sealed record AwardApprovedIntegrationEvent(
    Guid Id,
    DateTime OccurredOnUtc,
    string? TenantId,
    string CorrelationId,
    string Source,
    Guid RfqId,
    string RfqCode,
    Guid AwardId,
    string AwardCode,
    IReadOnlyList<Guid> VendorIds) : IIntegrationEvent;
