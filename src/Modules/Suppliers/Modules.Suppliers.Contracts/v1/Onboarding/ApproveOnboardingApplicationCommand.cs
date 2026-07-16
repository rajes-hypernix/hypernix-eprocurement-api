using FSH.Modules.Suppliers.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Suppliers.Contracts.v1.Onboarding;

public sealed record ApproveOnboardingApplicationCommand(Guid ApplicationId) : ICommand<OnboardingApproveResultDto>;
