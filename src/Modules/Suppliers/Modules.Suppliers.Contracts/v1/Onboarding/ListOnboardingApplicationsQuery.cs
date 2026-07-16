using FSH.Modules.Suppliers.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Suppliers.Contracts.v1.Onboarding;

/// <summary>The buyer queue — every application that has left Draft.</summary>
public sealed record ListOnboardingApplicationsQuery : IQuery<IReadOnlyList<OnboardingQueueItemDto>>;
