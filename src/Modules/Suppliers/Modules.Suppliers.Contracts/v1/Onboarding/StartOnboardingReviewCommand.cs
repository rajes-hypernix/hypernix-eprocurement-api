using FSH.Modules.Suppliers.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Suppliers.Contracts.v1.Onboarding;

public sealed record StartOnboardingReviewCommand(Guid ApplicationId) : ICommand<OnboardingApplicationDto>;
